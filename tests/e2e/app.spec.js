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
  await page.getByLabel("Load an example").selectOption("emoji");
  await page.locator("#encoding-heading").click();
  await page.getByRole("button", { name: /Select rune 1,/ }).focus();
  await expect(page.getByRole("button", { name: "Select text element 1" })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("table", { name: "UTF-16 code units in the string" }).locator("tbody tr.selected-row")).toHaveCount(2);
  await expect(page.getByLabel("UTF-8 byte selector").locator(".byte-chip.selected")).toHaveCount(4);
  await expect(page.locator(".selection-summary")).toContainText("Selected text element 1");
});

test("flags invisible, bidi, invalid, and mixed-script review cases", async ({ page }) => {
  await load(page);
  await page.getByLabel("Load an example").selectOption("bidi");
  await expect(page.getByRole("heading", { name: "Review these code points" })).toBeVisible();
  await expect(page.locator(".warning-panel")).toContainText("RIGHT-TO-LEFT OVERRIDE");
  await page.getByLabel("Load an example").selectOption("confusable");
  await expect(page.getByText(/not proof of spoofing/)).toBeVisible();
  await page.getByLabel("Load an example").selectOption("invalid");
  await expect(page.getByRole("button", { name: "Invalid UTF-16" })).toBeVisible();
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

test("shares fragment state and restores it on reload", async ({ page, context }) => {
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await load(page);
  await page.getByLabel("Text", { exact: true }).check();
  await page.getByRole("textbox", { name: "Text to inspect" }).fill("shared 🥰 value");
  await page.getByRole("button", { name: "Copy link" }).click();
  await expect(page).toHaveURL(/#state=/);
  const sharedUrl = page.url();
  expect(sharedUrl).not.toContain("shared%20");
  await page.goto(sharedUrl);
  await expect(page.getByRole("textbox", { name: "Text to inspect" })).toHaveValue("shared 🥰 value");
});

test("ignores malformed fragment state safely", async ({ page }) => {
  await page.goto("/#state=%25%25%25not-base64");
  await expect(page.getByRole("heading", { name: ".NET Unicode Explorer" })).toBeVisible({ timeout: 60_000 });
  await expect(page.getByRole("textbox", { name: "C# escaped string contents" })).toHaveValue("Café | Cafe\\u0301");
});

test("remembering is opt-in and saved work can be forgotten", async ({ page }) => {
  await load(page);
  await page.getByLabel("Remember on this device").check();
  await page.getByLabel("Text", { exact: true }).check();
  const input = page.getByRole("textbox", { name: "Text to inspect" });
  await input.fill("remember me");
  await input.blur();
  await page.reload();
  await expect(page.getByRole("textbox", { name: "Text to inspect" })).toHaveValue("remember me");
  await page.getByRole("button", { name: "Forget saved work" }).click();
  await page.reload();
  await expect(page.getByRole("textbox", { name: "C# escaped string contents" })).toHaveValue("Café | Cafe\\u0301");
  await expect(page.getByLabel("Remember on this device")).not.toBeChecked();
});

test("Ctrl+Enter analyzes without replacing the submit button", async ({ page }) => {
  await load(page);
  const input = page.getByLabel("C# escaped string contents");
  await input.fill("\\u12");
  await input.press("Control+Enter");
  await expect(page.locator("#input-error")).toContainText("exactly 4 hexadecimal digits");
  await expect(page.getByRole("button", { name: "Analyze string" })).toBeVisible();
});

test("uses the shared Swimburger shell token contract", async ({ page }) => {
  await load(page);
  const tokens = await page.evaluate(() => {
    const styles = getComputedStyle(document.documentElement);
    return ["--brand-darkest", "--brand-dark", "--brand-mid", "--brand-accent", "--color-terminal"].map(name => styles.getPropertyValue(name).trim());
  });
  expect(tokens).toEqual(["#4a0636", "#6c0a51", "#a02c56", "#ffc966", "#0d1117"]);
  await expect(page.getByRole("heading", { name: ".NET Unicode Explorer" })).toHaveCount(1);
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
