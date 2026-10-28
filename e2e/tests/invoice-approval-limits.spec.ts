import { expect, test, type APIRequestContext } from '@playwright/test';

const demoPassword = 'ChangeMe123!';

async function login(request: APIRequestContext, username: string): Promise<string> {
  const response = await request.post('/api/auth/login', {
    data: { username, password: demoPassword },
  });
  expect(response.ok()).toBeTruthy();
  const result = await response.json();
  return result.token as string;
}

async function getInvoice(
  request: APIRequestContext,
  invoiceId: string,
  token: string,
): Promise<Record<string, unknown>> {
  const response = await request.get(`/api/invoices/${invoiceId}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status()).toBe(200);
  return response.json();
}

async function deleteInvoicesForCustomer(
  request: APIRequestContext,
  customerName: string,
): Promise<void> {
  const adminToken = await login(request, 'demo.admin');
  const response = await request.get('/api/invoices', {
    headers: { Authorization: `Bearer ${adminToken}` },
  });
  if (!response.ok()) return;

  const invoices = await response.json() as Array<{ id: string; customerName: string }>;
  for (const invoice of invoices.filter((item) => item.customerName === customerName)) {
    await request.delete(`/api/invoices/${invoice.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
  }
}

test('legacy Approver over-limit denial preserves the invoice', async ({ page, request }) => {
  const customerName = `Stage 1 UI ${Date.now()}`;
  let invoiceId: string | undefined;

  try {
    await page.goto('/');
    await page.getByLabel('Username').fill('demo.approver');
    await page.getByLabel('Password').fill(demoPassword);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await page.getByLabel('Customer name').fill(customerName);
    await page.getByLabel('Amount').fill('50000.01');
    await page.getByRole('button', { name: 'Create invoice' }).click();

    const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
    await expect(invoiceRow).toBeVisible();
    invoiceId = (await invoiceRow.getByRole('cell').first().innerText()).trim();

    await page.getByRole('button', { name: `Submit invoice ${invoiceId} for approval` }).click();
    await page.getByRole('tab', { name: 'Approvals' }).click();
    await expect(page.getByRole('listitem').filter({ hasText: invoiceId })).toBeVisible();

    const sessionToken = await page.evaluate(() => {
      const session = JSON.parse(window.sessionStorage.getItem('session') ?? 'null');
      return session?.token as string | undefined;
    });
    expect(sessionToken).toBeTruthy();

    const before = await getInvoice(request, invoiceId, sessionToken!);
    expect(before).toMatchObject({
      amount: 50000.01,
      status: 'PendingApproval',
      decidedBy: null,
      decidedAt: null,
      decisionNote: null,
    });

    await page.getByRole('button', { name: `Approve invoice ${invoiceId}` }).click();
    await expect(
      page.getByRole('alert').filter({ hasText: 'Manager approval limit is $50,000.' }),
    ).toBeVisible();

    const after = await getInvoice(request, invoiceId, sessionToken!);
    expect(after).toEqual(before);
  } finally {
    await deleteInvoicesForCustomer(request, customerName);
  }
});

test('unsupported approval role receives an HTTP denial without changing the invoice', async ({ request }) => {
  const managerToken = await login(request, 'demo.approver');
  const unsupportedToken = await login(request, 'demo.unsupported');
  const customerName = `Stage 1 API ${Date.now()}`;

  const createResponse = await request.post('/api/invoices', {
    headers: { Authorization: `Bearer ${managerToken}` },
    data: { customerName, amount: 250 },
  });
  expect(createResponse.status()).toBe(201);
  const created = await createResponse.json();
  const invoiceId = created.id as string;

  try {
    const submitResponse = await request.post(`/api/invoices/${invoiceId}/submit`, {
      headers: { Authorization: `Bearer ${managerToken}` },
    });
    expect(submitResponse.ok()).toBeTruthy();

    const before = await getInvoice(request, invoiceId, managerToken);
    const deniedResponse = await request.post(`/api/approvals/${invoiceId}/approve`, {
      headers: { Authorization: `Bearer ${unsupportedToken}` },
    });
    expect(deniedResponse.status()).toBe(403);
    const denial = await deniedResponse.json();
    expect(denial.error).toBeTruthy();

    const after = await getInvoice(request, invoiceId, managerToken);
    expect(after).toEqual(before);
  } finally {
    await deleteInvoicesForCustomer(request, customerName);
  }
});
