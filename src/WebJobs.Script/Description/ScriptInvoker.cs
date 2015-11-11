﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script
{
    // TODO: make this internal
    public class ScriptInvoker : IFunctionInvoker
    {
        // TODO: Add support for php, sh
        private static string[] _supportedScriptTypes = new string[] { "ps1", "cmd", "bat", "py" };
        private readonly string _scriptFilePath;
        private readonly string _scriptType;

        public ScriptInvoker(string scriptFilePath)
        {
            _scriptFilePath = scriptFilePath;
            _scriptType = Path.GetExtension(_scriptFilePath).ToLower().TrimStart('.');
        }

        public static bool IsSupportedScriptType(string extension)
        {
            string scriptType = extension.ToLower().TrimStart('.');
            return _supportedScriptTypes.Contains(scriptType);
        }

        public async Task Invoke(object[] parameters)
        {
            string input = parameters[0].ToString();
            TextWriter textWriter = (TextWriter)parameters[1];

            switch (_scriptType)
            {
                case "ps1":
                    await InvokePowerShellScript(input, textWriter);
                    break;
                case "cmd":
                case "bat":
                    await InvokeWindowsBatchScript(input, textWriter);
                    break;
                case "py":
                    await InvokePythonScript(input, textWriter);
                    break;
            }
        }

        internal Task InvokePythonScript(string input, TextWriter textWriter)
        {
            string scriptHostArguments = string.Format("{0} \"{1}\"", _scriptFilePath, input);

            return InvokeScriptHostCore("python.exe", scriptHostArguments, textWriter);
        }

        internal Task InvokePowerShellScript(string input, TextWriter textWriter)
        {
            string scriptInputArguments = string.Format("-p1 \"{0}\"", input);
            string scriptHostArguments = string.Format("-ExecutionPolicy RemoteSigned -File {0} {1}", _scriptFilePath, scriptInputArguments);

            return InvokeScriptHostCore("PowerShell.exe", scriptHostArguments, textWriter);
        }

        internal Task InvokeWindowsBatchScript(string input, TextWriter textWriter)
        {
            string scriptInputArguments = string.Format("-p1 \"{0}\"", input);
            string scriptHostArguments = string.Format("/c {0} {1}", _scriptFilePath, scriptInputArguments);

            return InvokeScriptHostCore("cmd", scriptHostArguments, textWriter);
        }

        internal Task InvokeScriptHostCore(string path, string arguments, TextWriter textWriter)
        {
            string workingDirectory = Path.GetDirectoryName(_scriptFilePath);

            // TODO
            // - put a timeout on how long we wait?
            // - need to periodically flush the standard out to the TextWriter
            // - need to handle stderr as well
            Process process = CreateProcess(path, workingDirectory, arguments);
            process.Start();
            process.WaitForExit();

            // write the results to the Dashboard
            string output = process.StandardOutput.ReadToEnd();
            textWriter.Write(output);

            if (process.ExitCode != 0)
            {
                // TODO: handle/log failure
            }

            return Task.FromResult(0);
        }

        internal Process CreateProcess(string path, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables = null)
        {
            // TODO: need to set encoding on stdout/stderr?
            var psi = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                ErrorDialog = false,
                Arguments = arguments
            };

            if (environmentVariables != null)
            {
                foreach (var pair in environmentVariables)
                {
                    psi.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            return new Process()
            {
                StartInfo = psi
            };
        }
    }
}