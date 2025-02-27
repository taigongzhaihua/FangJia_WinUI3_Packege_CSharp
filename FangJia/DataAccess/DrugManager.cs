using System;
using System.IO;

namespace FangJia.DataAccess;

public class DrugManager
{
    private static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.db");
    private readonly string _connectionString = $"Data Source={DatabasePath};";

}