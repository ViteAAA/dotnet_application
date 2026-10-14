
var folderPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
    "YourAppName"
);

var filePath = Path.Combine(folderPath, "receipt_template.json");

if (File.Exists(filePath))
{
    // Читаем весь текст из файла и выводим в консоль
    var fileContent = File.ReadAllText(filePath);
    Console.WriteLine(fileContent);
}
else
{
    Console.WriteLine($"Файл не найден по пути: {filePath}");
}
