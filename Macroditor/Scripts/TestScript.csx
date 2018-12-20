using System.IO;
var res = string.Join(", ", InputText.Split(' ').Where(x => x.Length > 2));
File.WriteAllText("./Test.txt", res);

res