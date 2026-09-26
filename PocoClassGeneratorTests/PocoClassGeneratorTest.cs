using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Xunit;

namespace PocoClassGeneratorTests
{
    public class DataTablePocoClassGeneratorTest
    {
        [Fact]
        public void DataTablePocoClassTest()
        {
            var dt = new DataTable();
            dt.TableName = "TestTable";
            dt.Columns.Add(new DataColumn() { ColumnName = "ID", DataType = typeof(string) });

            var result = dt.GenerateClass();
            var expect =
@"public class TestTable
{
	public string ID { get; set; }
}";
            Assert.Equal(expect, result);
        }
    }

    public class PocoClassGeneratorTest
    {
        static readonly string _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=SSPI;Initial Catalog=GeneratorDataBase;";
        DbConnection GetConnection()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        [Fact]
        public void GenerateClassTest()
        {
            using (var conn = GetConnection())
            {
                var result = conn.GenerateClass("select * from table1");
                Console.WriteLine(result);

                Assert.Contains("public class table1", result);
            }

            using (var conn = GetConnection())
            {
                var result = conn.GenerateClass("select * from table1");
                Console.WriteLine(result);

                Assert.Contains("public class table1", result);
            }

            using (var conn = GetConnection())
            {
                var result = conn.GenerateClass("with cte as (select 1 id , 'weihan' name) select * from cte;");
                Console.WriteLine(result);

                Assert.Contains("public class Info", result);
            }
            using (var conn = GetConnection())
            {
                var result = conn.GenerateClass("with cte as (select 1 id , 'weihan' name) select * from cte;", "CteModel");
                Console.WriteLine(result);

                Assert.Contains("public class CteModel", result);
            }
        }

        [Fact]
        public void GenerateAllTables()
        {
            using (var conn = GetConnection())
            {
                var result = conn.GenerateAllTables();
                Console.WriteLine(result);

                Assert.Contains("public class table1", result);
                Assert.Contains("public class table2", result);
            }
        }

        [Fact]
        public void DapperContrib_GenerateAllTables_Test()
        {
            using (var conn = GetConnection())
            {
                var result = conn.GenerateAllTables(GeneratorBehavior.DapperContrib);
                Console.WriteLine(result);

                Assert.Contains("[Dapper.Contrib.Extensions.ExplicitKey]", result);
                Assert.Contains("public int ID { get; set; }", result);
                Assert.Contains("[Dapper.Contrib.Extensions.Computed]", result);
                Assert.Contains("public int AutoIncrementColumn { get; set; }", result);
                Assert.Contains("[Dapper.Contrib.Extensions.Table(\"table1\")]", result);
                Assert.Contains("[Dapper.Contrib.Extensions.Key]", result);
                Assert.Contains("public int ID { get; set; }", result);
            }
        }
        [Fact]
        public void DapperContribExtended_UniqueConstraints_GetSeparateBatchNumbers_Test()
        {
            using (var conn = GetConnection())
            {
                var result = conn.GenerateClass(
                    "select * from table3", "table3",
                    generatorBehavior: GeneratorBehavior.DapperContribExtended);
                Console.WriteLine(result);

                var codeBatch = System.Text.RegularExpressions.Regex.Match(
                    result, @"\[UniqueConstraint\((\d+)\)\](?:\s*\[[^\]]*\])*\s*public int Code").Groups[1].Value;
                var nameBatch = System.Text.RegularExpressions.Regex.Match(
                    result, @"\[UniqueConstraint\((\d+)\)\](?:\s*\[[^\]]*\])*\s*public string Name").Groups[1].Value;
                var groupBatch = System.Text.RegularExpressions.Regex.Match(
                    result, @"\[UniqueConstraint\((\d+)\)\](?:\s*\[[^\]]*\])*\s*public int GroupId").Groups[1].Value;
                var typeBatch = System.Text.RegularExpressions.Regex.Match(
                    result, @"\[UniqueConstraint\((\d+)\)\](?:\s*\[[^\]]*\])*\s*public int TypeId").Groups[1].Value;

                Assert.NotEqual(string.Empty, codeBatch);
                Assert.NotEqual(string.Empty, nameBatch);
                // دو قید مستقل نباید یک گروه شوند
                Assert.NotEqual(codeBatch, nameBatch);
                // ستون‌های یک قید مرکب باید یک batch مشترک داشته باشند
                Assert.Equal(groupBatch, typeBatch);
                Assert.NotEqual(string.Empty, groupBatch);
            }
        }

        [Fact]
        public void DapperContribExtended_SingleUniqueIndex_KeepsBareAttribute_Test()
        {
            using (var conn = GetConnection())
            {
                var result = conn.GenerateClass(
                    "select * from table4", "table4",
                    generatorBehavior: GeneratorBehavior.DapperContribExtended);
                Console.WriteLine(result);

                Assert.Contains("[UniqueConstraint]", result);
                Assert.DoesNotContain("[UniqueConstraint(", result);
            }
        }
    }
}
