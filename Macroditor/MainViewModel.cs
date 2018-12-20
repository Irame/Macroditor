using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Macroditor;
using Microsoft.CodeAnalysis;
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

        public MainViewModel()
        {
            Scripts = new ObservableCollection<Script>();
            
            NewScriptCommand = new RelayCommand(() =>
            {
                string fileName = "New Script.csx";
                int i = 1;
                while (File.Exists(Path.Combine(Script.BasePath, fileName)))
                    fileName = $"New Script ({i++}).csx";
                
                Scripts.Add(new Script(Path.Combine(Script.BasePath, fileName)));
            });
            
            OpenDirectroyCommand = new RelayCommand(() =>
            {
                if (!Directory.Exists(Script.BasePath))
                    Directory.CreateDirectory(Script.BasePath);
                Process.Start(Path.GetFullPath(Script.BasePath));
            });

            ReloadScriptsCommand = new RelayCommand(() => LoadAllScripts(Script.BasePath));

            LoadAllScripts(Script.BasePath);
        }

        private string[] _defaultReferencedAssemblies =
        {
            "mscorlib",
            "System.Core"
        };

        
        private string[] _defaultGlobalUsings =
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text.RegularExpressions"
        };

        public void LoadAllScripts(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
                directoryInfo.Create();

            if (!File.Exists(Script.ReferencedAssembliesFile))
                File.WriteAllText(Script.ReferencedAssembliesFile, string.Join(Environment.NewLine, _defaultReferencedAssemblies));

            if (!File.Exists(Script.GlobalUsingsFile))
                File.WriteAllText(Script.GlobalUsingsFile, string.Join(Environment.NewLine, _defaultGlobalUsings));

            FileInfo[] files = directoryInfo.GetFiles("*.csx");
            Scripts.Clear();
            foreach (var file in files)
            {
                Scripts.Add(new Script(file.FullName));
            }
        }
    }
    
    public class ScriptGlobals
    {
        public string InputText { get; set; }
    }

    class Script
    {
        public static string BasePath = "./Scripts";
        
        public static string ReferencedAssembliesFile => Path.Combine(BasePath, "_ReferencedAssemblies");
        public static string GlobalUsingsFile => Path.Combine(BasePath, "_GlobalUsings");

        public string Name { get; private set; }
        public string FilePath { get; private set; }
        
        public RelayCommand<TextBox> ExecuteScriptCommand { get; }

        public Script(string file)
        {
            if (Path.GetExtension(file) != ".csx")
                file += ".csx";

            if (!File.Exists(file))
                File.WriteAllText(file, "InputText");
            
            Name = Path.GetFileNameWithoutExtension(file);
            FilePath = file;

            ExecuteScriptCommand = new RelayCommand<TextBox>((textBox) =>
            {
                try
                {
                    var globals = new ScriptGlobals { InputText = textBox.Text };
                    var usings = File.ReadAllLines(GlobalUsingsFile).Where(IsGoodLine);
                    var references = File.ReadAllLines(ReferencedAssembliesFile).Where(IsGoodLine).Select(Assembly.Load);
                    var scriptOptions = ScriptOptions.Default.AddReferences(references).AddImports(usings);
                    
                    var result = CSharpScript.EvaluateAsync(File.ReadAllText(FilePath), globals: globals, options: scriptOptions).Result;
                    textBox.Text = $"{result}";
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
