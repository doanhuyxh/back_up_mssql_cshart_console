using Microsoft.SqlServer.Dac;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {


        string old_ip = "127.0.0.1";
        string old_user = "sa";
        string old_pass = "01882904300Huy@";
        string old_port = "1433";
        string default_backupDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "FileBackup", $"{old_ip.Replace(".", "_")}" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss"));
        string backupDirectoryPath = default_backupDirectoryPath;

        string new_ip = string.Empty;
        string new_user = string.Empty;
        string new_pass = string.Empty;
        string new_port = string.Empty;
        string check_new_backup = string.Empty;



        Console.Clear();

        Console.WriteLine("Database BackUp Tool");

        Console.WriteLine("Enter old connection details:\n");

        Console.Write("Enter old ip: ");
        old_ip = Console.ReadLine() ?? "";
        old_ip = old_ip.Trim();

        Console.Write("Enter old port: ");
        old_port = Console.ReadLine() ?? "";
        old_port = old_port.Trim();

        Console.Write("Enter old user: ");
        old_user = Console.ReadLine() ?? "";
        old_user = old_user.Trim();

        Console.Write("Enter old password: ");
        old_pass = Console.ReadLine() ?? "";
        old_pass = old_pass.Trim();

        Console.Write("Enter backup directory path: ");
        backupDirectoryPath = Console.ReadLine() ?? "";

        if (string.IsNullOrEmpty(backupDirectoryPath))
        {
            backupDirectoryPath = default_backupDirectoryPath;
        }
        backupDirectoryPath = backupDirectoryPath.Trim();

        if (!Directory.Exists(backupDirectoryPath))
        {
            Directory.CreateDirectory(backupDirectoryPath);
        }
        Console.WriteLine("\n--------------------");
        Console.WriteLine("Do you want to export databases to a new server? (y/n)");
        check_new_backup = Console.ReadLine() ?? "";
        check_new_backup = check_new_backup.Trim();
        if (check_new_backup.ToLower() == "y")
        {
            Console.WriteLine("Enter new connection details:");

            Console.Write("Enter new ip: ");
            new_ip = Console.ReadLine() ?? "";
            new_ip = new_ip.Trim();

            Console.Write("Enter new port: ");
            new_port = Console.ReadLine() ?? "";
            new_port = new_port.Trim();

            Console.Write("Enter new user: ");
            new_user = Console.ReadLine() ?? "";
            new_user = new_user.Trim();

            Console.Write("Enter new password: ");
            new_pass = Console.ReadLine() ?? "";
            new_pass = new_pass.Trim();

            Console.WriteLine("\n--------------------");
        }

        Console.WriteLine("Exporting databases...");
        await ExportDatabases(backupDirectoryPath, old_ip, old_port, old_user, old_pass);
        Console.WriteLine("Export completed.");
        if (check_new_backup.ToLower() == "y")
        {
            Console.WriteLine("\n--------------------\n");
            Console.WriteLine("Importing databases...");
            await CallImport(backupDirectoryPath, new_ip, new_port, new_user, new_pass);
            Console.WriteLine("Import completed.");
        }

    }
    private static async Task ExportDatabases(string backupDirectory, string ip, string port, string user, string pass)
    {
        string connectionString = $"Server={ip},{port};User Id={user};Password={pass};";

        using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')";
            var reader = await command.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                Console.WriteLine("No database found");
                return;
            }
            else
            {
                //show list database
                while (await reader.ReadAsync())
                {
                    string databaseName = reader.GetString(0);
                    Console.WriteLine(databaseName);
                }
            }

            while (await reader.ReadAsync())
            {
                string databaseName = reader.GetString(0);
                string bacpacFilePath = Path.Combine(backupDirectory, $"{databaseName}.bacpac");
                Console.WriteLine($"Exporting {databaseName} to {bacpacFilePath}");
                connectionString = $"Data Source={ip};Initial Catalog={databaseName};Persist Security Info=True;TrustServerCertificate=True; User ID={user};Password={pass};";
                DacServices dacServices = new DacServices(connectionString);
                dacServices.ExportBacpac(bacpacFilePath, databaseName);
            }
        }
    }
    private static async Task CallImport(string backUpDirectory, string ip, string port, string user, string pass)
    {
        try
        {
            string[] files = Directory.GetFiles(backUpDirectory);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

                await ImportAsync(file, ip, port, fileName.Replace(".bacpac", ""), user, pass);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving files: {ex.Message}");
        }
    }

    private static async Task ImportAsync(string bacpacFilePath, string ip, string port, string databaseName, string user, string pass)
    {

        string targetConnectionString = $"Server={ip};Database={databaseName};User Id={user};Password={pass};Persist Security Info=True;TrustServerCertificate=True;";
        Console.WriteLine($"Importing {bacpacFilePath} to {databaseName}...{targetConnectionString}");
        try
        {
            DacServices dacServices = new DacServices(targetConnectionString);
            dacServices.Message += (sender, e) => Console.WriteLine(e.Message);

            using (BacPackage bacPackage = BacPackage.Load(bacpacFilePath))
            {
                await Task.Run(() => dacServices.ImportBacpac(bacPackage, databaseName, null));
            }

            Console.WriteLine("Database imported successfully.");
        }
        catch (DacServicesException dacEx)
        {
            Console.WriteLine($"Error importing bacpac: {dacEx.Message}");
            foreach (var message in dacEx.Messages)
            {
                Console.WriteLine(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error importing bacpac: {ex.Message}");
        }
    }
}