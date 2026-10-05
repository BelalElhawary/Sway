using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sway.Example.Web;
using Sway.Media;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The browser media engine loads a script first, so it is installed before the app starts.
await BrowserMediaBackend.InstallAsync();
await builder.Build().RunAsync();
