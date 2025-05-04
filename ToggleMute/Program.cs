using System.Net.Http;

const string url = "http://localhost:5288/ToggleMute";
using HttpClient client = new();
// await is needed, otherwise the client gets disposed before the service responds
_ = await client.GetAsync(url);
