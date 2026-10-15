// Blazor JS initializer, loaded automatically by blazor.web.js.
// Applies the runtime config rendered by <BetterCacheHead /> before the WebAssembly runtime is created.
//
// .NET 9 notes: beforeWebAssemblyStart of a web initializer never runs when Blazor autostarts, so this uses
// beforeWebStart. It configures the dotnet.js builder (the same ES module instance blazor.web.js imports
// later); keys absent from blazor.boot.json, such as disableNoCacheFetch, survive the boot manifest merge.
export async function beforeWebStart() {
  const meta = document.querySelector('meta[name="better-cache-webassembly-config"]');

  if (!meta) {
    return;
  }

  try {
    const { dotnetJs, config } = JSON.parse(meta.content);
    const { dotnet } = await import(new URL(dotnetJs, document.baseURI).toString());

    dotnet.withConfig(config);
  } catch (error) {
    console.warn('BetterCache: could not configure the WebAssembly runtime; its default caching applies.', error);
  }
}
