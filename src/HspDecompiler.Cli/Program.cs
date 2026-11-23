using HspDecompiler.Cli;

return await new DecompileCommand().Parse(args).InvokeAsync().ConfigureAwait(false);
