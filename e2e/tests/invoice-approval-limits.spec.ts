import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const demoPassword = 'ChangeMe123!';

type ApprovalHistoryRecord = {
  username: string;
  role: string;
  outcome: 'Approved' | 'Rejected';
  decidedAt: string;
  reason: string | null;
};

type InvoiceResponse = {
  amount: number;
  status: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  approvalHistory: ApprovalHistoryRecord[];
};

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
): Promise<InvoiceResponse> {
  const response = await request.get(`/api/invoices/${invoiceId}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status()).toBe(200);
  return response.json() as Promise<InvoiceResponse>;
}

async function signInThroughUi(page: Page, username: string): Promise<void> {
  await page.goto('/');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(demoPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

async function deleteInvoicesForCustomer(
  request: APIRequestContext,
  customerName: string,
): Promise<void> {
  const adminToken = await login(request, 'demo.admin');
  const response = await request.get('/api/invoices', {
    headers: { Authorization: `Bearer ${adminToken}` },
  });
  expect(response.status()).toBe(200);

  const invoices = await response.json() as Array<{ id: string; customerName: string }>;
  for (const invoice of invoices.filter((item) => item.customerName === customerName)) {
    const deleteResponse = await request.delete(`/api/invoices/${invoice.id}`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
    expect(deleteResponse.status()).toBe(204);
  }
}

test('legacy Approver over-limit denial preserves the invoice', async ({ page, request }) => {
  const customerName = `Stage 1 UI ${Date.now()}`;
  let invoiceId: string | undefined;

  try {
    await signInThroughUi(page, 'demo.approver');

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
  let invoiceId: string | undefined;

  try {
    const createResponse = await request.post('/api/invoices', {
      headers: { Authorization: `Bearer ${managerToken}` },
      data: { customerName, amount: 250 },
    });
    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    invoiceId = created.id as string;

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

test('high-value first approval remains pending until a distinct Director approves', async ({ page, request }) => {
  const customerName = `Stage 2 UI ${Date.now()}`;

  try {
    await signInThroughUi(page, 'demo.manager');
    await page.getByLabel('Customer name').fill(customerName);
    await page.getByLabel('Amount').fill('500000.01');
    await page.getByRole('button', { name: 'Create invoice' }).click();

    const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
    await expect(invoiceRow).toBeVisible();
    const invoiceId = (await invoiceRow.getByRole('cell').first().innerText()).trim();
    await page.getByRole('button', { name: `Submit invoice ${invoiceId} for approval` }).click();
    await page.getByRole('tab', { name: 'Approvals' }).click();

    const managerToken = await page.evaluate(() => {
      const session = JSON.parse(window.sessionStorage.getItem('session') ?? 'null');
      return session?.token as string | undefined;
    });
    expect(managerToken).toBeTruthy();

    await page.getByRole('button', { name: `Approve invoice ${invoiceId}` }).click();
    const pendingItem = page.getByRole('listitem').filter({ hasText: invoiceId });
    await expect(pendingItem).toContainText('Awaiting second Director approval');

    const afterFirst = await getInvoice(request, invoiceId, managerToken!);
    expect(afterFirst).toMatchObject({
      status: 'PendingApproval',
      decidedBy: null,
      decidedAt: null,
      decisionNote: null,
    });
    const firstApproval = afterFirst.approvalHistory[0];
    expect(afterFirst.approvalHistory).toHaveLength(1);
    expect(firstApproval).toMatchObject({ username: 'demo.manager', role: 'Manager', outcome: 'Approved' });

    await page.getByRole('button', { name: 'Sign out' }).click();
    await signInThroughUi(page, 'demo.director');
    await page.getByRole('tab', { name: 'Approvals' }).click();
    const directorToken = await page.evaluate(() => {
      const session = JSON.parse(window.sessionStorage.getItem('session') ?? 'null');
      return session?.token as string | undefined;
    });
    expect(directorToken).toBeTruthy();
    await page.getByRole('button', { name: `Approve invoice ${invoiceId}` }).click();
    await expect(page.getByRole('listitem').filter({ hasText: invoiceId })).toHaveCount(0);

    const completed = await getInvoice(request, invoiceId, directorToken!);
    expect(completed.status).toBe('Approved');
    expect(completed.decidedBy).toBe('demo.director');
    expect(completed.approvalHistory).toHaveLength(2);
    expect(completed.approvalHistory.map((entry) => entry.username)).toEqual([
      'demo.manager',
      'demo.director',
    ]);
  } finally {
    await deleteInvoicesForCustomer(request, customerName);
  }
});

test('same user and non-Director second approvals are rejected without mutation', async ({ request }) => {
  const directorToken = await login(request, 'demo.director');
  const adminToken = await login(request, 'demo.admin');
  const customerName = `Stage 2 API ${Date.now()}`;

  try {
    const createResponse = await request.post('/api/invoices', {
      headers: { Authorization: `Bearer ${directorToken}` },
      data: { customerName, amount: 600000 },
    });
    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    const invoiceId = created.id as string;

    const submitResponse = await request.post(`/api/invoices/${invoiceId}/submit`, {
      headers: { Authorization: `Bearer ${directorToken}` },
    });
    expect(submitResponse.ok()).toBeTruthy();
    const firstApprovalResponse = await request.post(`/api/approvals/${invoiceId}/approve`, {
      headers: { Authorization: `Bearer ${directorToken}` },
    });
    expect(firstApprovalResponse.ok()).toBeTruthy();
    const beforeDenials = await getInvoice(request, invoiceId, directorToken);
    expect(beforeDenials.approvalHistory).toHaveLength(1);

    for (const token of [directorToken, adminToken]) {
      const denied = await request.post(`/api/approvals/${invoiceId}/approve`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      expect(denied.status()).toBe(409);
      expect((await denied.json()).error).toBeTruthy();
      expect(await getInvoice(request, invoiceId, directorToken)).toEqual(beforeDenials);
    }
  } finally {
    await deleteInvoicesForCustomer(request, customerName);
  }
});

test('authorized rejection after first approval retains and appends approval history', async ({ page, request }) => {
  const customerName = `Stage 2 rejection ${Date.now()}`;

  try {
    await signInThroughUi(page, 'demo.manager');
    await page.getByLabel('Customer name').fill(customerName);
    await page.getByLabel('Amount').fill('600000');
    await page.getByRole('button', { name: 'Create invoice' }).click();

    const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
    await expect(invoiceRow).toBeVisible();
    const invoiceId = (await invoiceRow.getByRole('cell').first().innerText()).trim();
    await page.getByRole('button', { name: `Submit invoice ${invoiceId} for approval` }).click();
    await page.getByRole('tab', { name: 'Approvals' }).click();

    const managerToken = await page.evaluate(() => {
      const session = JSON.parse(window.sessionStorage.getItem('session') ?? 'null');
      return session?.token as string | undefined;
    });
    expect(managerToken).toBeTruthy();
    await page.getByRole('button', { name: `Approve invoice ${invoiceId}` }).click();

    const afterFirst = await getInvoice(request, invoiceId, managerToken!);
    expect(afterFirst.status).toBe('PendingApproval');
    expect(afterFirst.approvalHistory).toHaveLength(1);

    await page.getByRole('button', { name: 'Sign out' }).click();
    await signInThroughUi(page, 'demo.admin');
    await page.getByRole('tab', { name: 'Approvals' }).click();
    const reason = 'Duplicate invoice';
    await page.getByLabel(`Rejection reason for ${invoiceId}`).fill(reason);
    await page.getByRole('button', { name: `Reject invoice ${invoiceId}` }).click();
    await expect(page.getByRole('listitem').filter({ hasText: invoiceId })).toHaveCount(0);

    const adminToken = await page.evaluate(() => {
      const session = JSON.parse(window.sessionStorage.getItem('session') ?? 'null');
      return session?.token as string | undefined;
    });
    expect(adminToken).toBeTruthy();
    const rejected = await getInvoice(request, invoiceId, adminToken!);
    expect(rejected.status).toBe('Rejected');
    expect(rejected.decidedBy).toBe('demo.admin');
    expect(rejected.decisionNote).toBe(reason);
    expect(rejected.approvalHistory).toHaveLength(2);
    expect(rejected.approvalHistory[0]).toEqual(afterFirst.approvalHistory[0]);
    expect(rejected.approvalHistory[1]).toMatchObject({
      username: 'demo.admin',
      role: 'Admin',
      outcome: 'Rejected',
      reason,
    });
  } finally {
    await deleteInvoicesForCustomer(request, customerName);
  }
});
