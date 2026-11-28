import { expect, test } from "@playwright/test";

async function load(page) {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: ".NET Unicode Explorer" })).toBeVisible({ timeout: 60_000 });
}

test("explains one string through related .NET views", async ({ page }) => {
  await load(page);
  await expect(page.getByText("UTF-16 code units", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("table", { name: "Text elements and their UTF-16 ranges" })).toBeVisible();
  await expect(page.getByRole("table", { name: "Rune values in the string" })).toBeVisible();
  await expect(page.getByRole("table", { name: "UTF-16 code units in the string" })).toBeVisible();
  await page.locator("#encoding-heading").click();
  await expect(page.locator(".encoding-grid")).toBeVisible();
  await page.getByText("Unicode normalization", { exact: true }).click();
  await expect(page.locator(".normalization-grid")).toBeVisible();
  await page.getByText(".NET Unicode quick reference", { exact: true }).click();
  await expect(page.getByRole("table", { name: ".NET string and Unicode abstractions" })).toBeVisible();
});

test("switches examples and keeps synchronized selections", async ({ page }) => {
  await load(page);
  await page.getByLabel("Load an example").selectOption("family");
  await expect(page.locator(".summary-grid > div").filter({ hasText: "string.Length" }).locator("dd")).toHaveText("11");
  await expect(page.getByRole("button", { name: /#0/ })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("table", { name: "Rune values in the string" }).locator("tbody tr")).toHaveCount(7);
  await expect(page.getByRole("table", { name: "UTF-16 code units in the string" }).locator("tbody tr")).toHaveCount(11);
});

test("parses C# escapes and defers errors until interaction completes", async ({ page }) => {
  await load(page);
  await page.getByLabel("C# escapes").check();
  const input = page.getByLabel("C# escaped string contents");
  await input.fill("\\u12");
  await expect(page.locator("#input-error")).toHaveCount(0);
  await input.blur();
  await expect(page.locator("#input-error")).toContainText("exactly 4 hexadecimal digits");
  await input.fill("\\U0001F970");
  await expect(page.locator("#input-error")).toHaveCount(0);
  await expect(page.getByRole("table", { name: "Rune values in the string" })).toContainText("U+1F970");
});

test("copies with visible accessible feedback", async ({ page, context }) => {
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await load(page);
  await page.getByRole("button", { name: "Copy escaped C#" }).click();
  await expect(page.getByRole("status")).toContainText("Copied escaped C# string");
});

test("is responsive, keyboard focusable, and free of horizontal page overflow", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await load(page);
  await page.getByLabel("Text", { exact: true }).check();
  await expect(page.getByRole("textbox", { name: "Text to inspect" })).toBeVisible();
  await page.getByRole("button", { name: "Analyze string" }).focus();
  await expect(page.getByRole("button", { name: "Analyze string" })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
  expect(await page.locator('meta[name="viewport"]').getAttribute("content")).not.toMatch(/maximum-scale|user-scalable/i);
});

test("serves security and cache headers", async ({ request }) => {
  const response = await request.get("/");
  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toContain("must-revalidate");
  expect(response.headers()["x-content-type-options"]).toBe("nosniff");
  expect(response.headers()["referrer-policy"]).toBe("strict-origin-when-cross-origin");
});
