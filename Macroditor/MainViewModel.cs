using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Macroditor;
using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace Macroditor
{
    class MainViewModel : PropertyChangedBase
    {
        public ObservableCollection<Script> Scripts { get; }

        public RelayCommand NewScriptCommand { get; }

        public RelayCommand ReloadScriptsCommand { get; }

        private string ScriptPath = "./Scripts";

        public MainViewModel()
        {
            Scripts = new ObservableCollection<Script>();
            
            NewScriptCommand = new RelayCommand(() =>
            {
                string fileName = "New Script.cs";
                int i = 1;
                while (File.Exists(Path.Combine(ScriptPath, fileName)))
                    fileName = $"New Script ({i++}).cs";
                
                Scripts.Add(new Script(Path.Combine(ScriptPath, fileName)));
            });

            ReloadScriptsCommand = new RelayCommand(() => LoadAllScripts(ScriptPath));

            LoadAllScripts(ScriptPath);
        }

        public void LoadAllScripts(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
                directoryInfo.Create();
            FileInfo[] files = directoryInfo.GetFiles("*.cs");
            Scripts.Clear();
            foreach (var file in files)
            {
                Scripts.Add(new Script(file.FullName));
            }
        }
    }

    class Script
    {
        class Globals
        {
            public string InputText;

            public override string ToString()
            {
                return $"const string InputText = @\"{InputText.Replace("\"", "\"\"")}\";\n";
            }
        }

        public string Name { get; private set; }
        public string FilePath { get; private set; }
        
        public RelayCommand<TextBox> ExecuteScriptCommand { get; }

        public Script(string file)
        {
            if (Path.GetExtension(file) != ".cs")
                file += ".cs";

            if (!File.Exists(file))
                File.WriteAllText(file, "InputText");

            Name = Path.GetFileNameWithoutExtension(file);
            FilePath = file;

            ExecuteScriptCommand = new RelayCommand<TextBox>((textBox) =>
            {
                var globals = new Globals { InputText = textBox.Text };
                var result = CSharpScript.EvaluateAsync(globals.ToString() + File.ReadAllText(FilePath)).Result;
                textBox.Text = $"{result}";
            });
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
