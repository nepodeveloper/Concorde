var builder = WebApplication.CreateBuilder(args); var app = builder.Build(); app.MapGet(/`, () => "Concorde API"); app.Run();
