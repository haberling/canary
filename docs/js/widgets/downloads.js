// Shared behavior for the downloads widget (see downloads.html for the
// template/data contract) -- referenced once per page via <script defer>,
// not duplicated per widget instance. Event delegation on `document` means
// a widget instance added later (hybrid mode's fragment-fetch swap) is
// handled automatically, no re-init call needed anywhere in the router
// code.
document.addEventListener("click", (e) => {
  const button = e.target.closest(".copy-command-button");
  if (!button) return;

  const command = button.closest(".copy-command")?.dataset.command;
  if (!command) return;

  navigator.clipboard.writeText(command);
  const original = button.textContent;
  button.textContent = "Copied!";
  setTimeout(() => {
    button.textContent = original;
  }, 1500);
});
