using GhEntraFederation;

var app = new CliApplication(new SystemProcessRunner(), new SystemConsole(), new RuntimeEnvironment());
return await app.RunAsync(args, CancellationToken.None);
