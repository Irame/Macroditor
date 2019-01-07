using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Macroditor
{
    class MainViewModel : PropertyChangedBase
    {
        public ObservableCollection<Script> Scripts { get; }

        public RelayCommand NewScriptCommand { get; }

        public RelayCommand OpenDirectroyCommand { get; }

        public RelayCommand ReloadScriptsCommand { get; }

        
        public string BasePath => "./Scripts";

        public string MacroditorPath => Path.Combine(BasePath, ".Macroditor");
        
        public string ReferencedAssembliesFile => Path.Combine(MacroditorPath, "ReferencedAssemblies");

        public string GlobalUsingsFile => Path.Combine(MacroditorPath, "GlobalUsings");

        public MainViewModel()
        {
            Scripts = new ObservableCollection<Script>();
            
            NewScriptCommand = new RelayCommand(() =>
            {
                string fileName = "New Script.cs";
                int i = 1;
                while (File.Exists(Path.Combine(BasePath, fileName)))
                    fileName = $"New Script ({i++}).cs";
                
                Scripts.Add(new Script(Path.Combine(BasePath, fileName), ReferencedAssembliesFile, GlobalUsingsFile));
            });
            
            OpenDirectroyCommand = new RelayCommand(() =>
            {
                if (!Directory.Exists(BasePath))
                    Directory.CreateDirectory(BasePath);
                Process.Start(Path.GetFullPath(BasePath));
            });

            ReloadScriptsCommand = new RelayCommand(() => LoadAllScripts());

            LoadAllScripts();
        }

        public void LoadAllScripts()
        {
            DirectoryInfo basePathInfo = new DirectoryInfo(BasePath);
            if (!basePathInfo.Exists)
                basePathInfo.Create();
            
            DirectoryInfo macroditorPathInfo = new DirectoryInfo(MacroditorPath);
            if (!macroditorPathInfo.Exists)
                macroditorPathInfo.Create();

            foreach (var manifestResourceName in typeof(MainViewModel).Assembly.GetManifestResourceNames())
            {
                Debug.WriteLine(manifestResourceName);
            }

            WriteResourceToFile(ReferencedAssembliesFile, "Macroditor.MacroditorScripsResources.ReferencedAssembliesFile", overrideExisting: false);
            WriteResourceToFile(GlobalUsingsFile, "Macroditor.MacroditorScripsResources.GlobalUsings", overrideExisting: false);

            IEnumerable<FileInfo> files = basePathInfo.GetFiles("*.csx").Concat(basePathInfo.GetFiles("*.cs"));
            Scripts.Clear();
            foreach (var file in files)
            {
                Scripts.Add(new Script(file.FullName, ReferencedAssembliesFile, GlobalUsingsFile));
            }
        }

        private void WriteResourceToFile(string path, string resource, bool overrideExisting)
        {
            if (overrideExisting || !File.Exists(path))
            using (Stream stream = typeof(MainViewModel).Assembly.GetManifestResourceStream(resource))
            using (var fileStream = File.Create(path))
            {
                stream.CopyTo(fileStream);
            }
        }
    }
    
    public class ScriptGlobals
    {
        public string InputText { get; set; }
        public string OutputText { get; set; }
    }

    class Script
    {

        public string Name { get; private set; }
        public string FilePath { get; private set; }
        
        public RelayCommand<TextBox> ExecuteScriptCommand { get; }

        public Script(string file, string referencedAssembliesFile, string globalUsingsFile)
        {
            if (!File.Exists(file))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"//css_ac freestyle");
                foreach (var line in File.ReadLines(globalUsingsFile))
                {
                    sb.AppendLine($"using {line};");
                }
                sb.AppendLine();
                sb.AppendLine("// for Autocomplete");
                sb.AppendLine(@"//string InputText = """";");
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("string OutputText = InputText;");

                File.WriteAllText(file, sb.ToString());
            }
            
            Name = Path.GetFileNameWithoutExtension(file);
            FilePath = file;


            ExecuteScriptCommand = new RelayCommand<TextBox>((textBox) =>
            {
                try
                {
                    var globals = new ScriptGlobals { InputText = textBox.Text };
                    var usings = File.ReadAllLines(globalUsingsFile).Select(x => x.Trim()).Where(IsGoodLine);
                    var references = File.ReadAllLines(referencedAssembliesFile).Select(x => x.Trim()).Where(IsGoodLine).Select(Assembly.Load);
                    var scriptOptions = ScriptOptions.Default.AddReferences(references).AddImports(usings);

                    var scriptContent = File.ReadAllText(FilePath);
                    scriptContent = Regex.Replace(scriptContent, "string InputText[^;]*;", "");

                    var scriptState = CSharpScript.RunAsync(scriptContent, globals: globals, options: scriptOptions).Result;
                    var outputText = scriptState.GetVariable("OutputText");
                    if (outputText == null)
                    {
                        MessageBox.Show($"The Script ({Name}) needs to set a variable named 'OutputText'.", "OutputText not set.");
                    }
                    else
                    {
                        textBox.Text = $"{outputText.Value.ToString()}";
                    }
                }
                catch (CompilationErrorException e)
                {
                    MessageBox.Show(string.Join(Environment.NewLine, e.Diagnostics), $"Compilation error in '{Name}'", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            bool IsGoodLine(string line)
            {
                return !(string.IsNullOrEmpty(line) || line.StartsWith("#"));
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
