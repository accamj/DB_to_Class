using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

[Flags]
public enum GeneratorBehavior
{
	Default = 0x0,
	View = 0x1,
	DapperContrib = 0x2,
	Comment = 0x4,
	DapperContribExtended = 0xA,
}

public static partial class PocoClassGenerator
{
	#region Property
	private static readonly Dictionary<Type, string> TypeAliases = new Dictionary<Type, string> {
		{ typeof(int), "int" },
		{ typeof(short), "short" },
		{ typeof(byte), "byte" },
		{ typeof(byte[]), "byte[]" },
		{ typeof(long), "long" },
		{ typeof(double), "double" },
		{ typeof(decimal), "decimal" },
		{ typeof(float), "float" },
		{ typeof(bool), "bool" },
		{ typeof(string), "string" }
	};

	private static readonly Dictionary<string, string> QuerySqls = new Dictionary<string, string> {
		{ "sqlconnection", "select  *  from [{0}] where 1=2" },
		{ "sqlceserver", "select  *  from [{0}] where 1=2" },
		{ "sqliteconnection", "select  *  from [{0}] where 1=2" },
		{ "oracleconnection", "select  *  from \"{0}\" where 1=2" },
		{ "mysqlconnection", "select  *  from `{0}` where 1=2" },
		{ "npgsqlconnection", "select  *  from \"{0}\" where 1=2" }
	};

	private static readonly Dictionary<string, string> TableSchemaSqls = new Dictionary<string, string> {
		{ "sqlconnection", "select TABLE_NAME from INFORMATION_SCHEMA.TABLES where TABLE_TYPE = 'BASE TABLE'" },
		{ "sqlceserver", "select TABLE_NAME from INFORMATION_SCHEMA.TABLES  where TABLE_TYPE = 'BASE TABLE'" },
		{ "sqliteconnection", "SELECT name FROM sqlite_master where type = 'table'" },
		{ "oracleconnection", "select TABLE_NAME from USER_TABLES where table_name not in (select View_name from user_views)" },
		{ "mysqlconnection", "select TABLE_NAME from  information_schema.tables where table_type = 'BASE TABLE'" },
		{ "npgsqlconnection", "select table_name from information_schema.tables where table_type = 'BASE TABLE'" }
	};


	private static readonly HashSet<Type> NullableTypes = new HashSet<Type> {
		typeof(int),
		typeof(short),
		typeof(long),
		typeof(double),
		typeof(decimal),
		typeof(float),
		typeof(bool),
		typeof(DateTime)
	};
	#endregion

	public static string GenerateAllTables(this System.Data.Common.DbConnection connection, GeneratorBehavior generatorBehavior = GeneratorBehavior.Default)
	{
		if (connection.State != ConnectionState.Open) connection.Open();

		var conneciontName = connection.GetType().Name.ToLower();
		var tables = new List<string>();
		var sql = generatorBehavior.HasFlag(GeneratorBehavior.View) ? TableSchemaSqls[conneciontName].Split("where")[0] : TableSchemaSqls[conneciontName];
		using (var command = connection.CreateCommand(sql))
		using (var reader = command.ExecuteReader())
		{
			while (reader.Read())
				tables.Add(reader.GetString(0));
		}

		var sb = new StringBuilder();
		sb.AppendLine("namespace Models { ");
		for (var index = 0; index < tables.Count; index++)
		{
			var table = tables[index];
			sb.Append(connection.GenerateClass(
				string.Format(QuerySqls[conneciontName], table), table, generatorBehavior: generatorBehavior
			));
		}

		sb.AppendLine("}");
		return sb.ToString();
	}

	public static string GenerateClass(this IDbConnection connection, string sql, GeneratorBehavior generatorBehavior)
		=> connection.GenerateClass(sql, null, generatorBehavior);

	public static string GenerateClass(this IDbConnection connection, string sql, string className = null, GeneratorBehavior generatorBehavior = GeneratorBehavior.Default)
	{
		if (connection.State != ConnectionState.Open) connection.Open();

		var builder = new StringBuilder();

		string tableName;
		//Get Table Name
		//Fix : [When View using CommandBehavior.KeyInfo will get duplicate columns �P Issue #8 �P shps951023/PocoClassGenerator](https://github.com/shps951023/PocoClassGenerator/issues/8 )
		var isFromMultiTables = false;
		using (var command = connection.CreateCommand(sql))
		using (var reader = command.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SingleRow))
		{
			var tables = reader.GetSchemaTable().Select().Select(s => s["BaseTableName"] as string).Distinct();
			tableName = string.IsNullOrWhiteSpace(className) ? tables.First() ?? "Info" : className;

			isFromMultiTables = tables.Count() > 1;

			if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContrib) && !isFromMultiTables)
				builder.AppendFormat("	[Dapper.Contrib.Extensions.Table(\"{0}\")]{1}", tableName, Environment.NewLine);
			builder.AppendFormat("	public class {0}{1}", tableName.Replace(" ", ""), Environment.NewLine);
			builder.AppendLine("	{");
		}

		// Get Unique Columns grouped by unique index.
		// هر ایندکس یکتای مجزا باید batch number جدا بگیرد؛ وگرنه همه ستون‌ها در یک گروه
		// مرکب دیده می‌شوند (باگ: دو قید تک‌ستونه مستقل مثل کد و نام فروشنده یک قید مرکب می‌شدند).
		var uniqueColumns = new List<Tuple<string, string>>();
		if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContribExtended))
		{
			string sqlUniqueColumns = $@"SELECT 
									     IndexName = ind.name,
									     ColumnName = col.name
									FROM 
									     sys.indexes ind 
									INNER JOIN 
									     sys.index_columns ic ON  ind.object_id = ic.object_id and ind.index_id = ic.index_id 
									INNER JOIN 
									     sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id 
									INNER JOIN 
									     sys.tables t ON ind.object_id = t.object_id 
									WHERE 
									     ind.is_primary_key = 0 
									     AND ind.is_unique = 1
									     AND ic.is_included_column = 0
										 And t.name = '{tableName}'
									ORDER BY ind.name, ic.key_ordinal";

			using var command = connection.CreateCommand(sqlUniqueColumns);
			using var reader = command.ExecuteReader();
			while (reader.Read())
				uniqueColumns.Add(Tuple.Create(reader.GetString(0), reader.GetString(1)));
		}
		var uniqueIndexNames = uniqueColumns.Select(u => u.Item1).Distinct().ToList();
		
		// Get default Columns
		var defaultColumns = new List<Tuple<string, string>>();
		if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContribExtended))
		{
			string sqlUniqueColumns = $@"SELECT COLUMN_NAME, COLUMN_DEFAULT
										 FROM INFORMATION_SCHEMA.COLUMNS
										 WHERE
										     TABLE_NAME = '{tableName}' AND
										     COLUMN_DEFAULT is not null ";

			using var command = connection.CreateCommand(sqlUniqueColumns);
			using var reader = command.ExecuteReader();
			while (reader.Read())
				defaultColumns.Add(new Tuple<string, string>(reader.GetString(0), reader.GetString(1)));
		}

		// get foreign key columns
		var foreignKeyColumns = new List<Tuple<string, string, string, string>>();
		if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContribExtended))
		{
			string sqlForeignKeys = $@"SELECT 
										    FK = fk.name, 
										    FKTable = OBJECT_NAME(fkcol.[object_id]),
										    FKCol = fkcol.name,
										    PKTable = OBJECT_NAME(pkcol.[object_id]),
										    PKCol = pkcol.name
										FROM sys.foreign_keys AS fk
										INNER JOIN sys.foreign_key_columns AS fkc
										ON fk.[object_id] = fkc.constraint_object_id
										INNER JOIN sys.columns AS fkcol
										ON fkc.parent_object_id = fkcol.[object_id]
										AND fkc.parent_column_id = fkcol.column_id
										INNER JOIN sys.columns AS pkcol
										ON fkc.referenced_object_id = pkcol.[object_id]
										AND fkc.referenced_column_id = pkcol.column_id
										where OBJECT_NAME(fkcol.[object_id]) = '{tableName}'
										ORDER BY fkc.constraint_column_id;";

			using (var command = connection.CreateCommand(sqlForeignKeys))
			using (var reader = command.ExecuteReader())
			{
				while (reader.Read())
					foreignKeyColumns.Add(new Tuple<string, string, string, string>(reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(0)));
			}
		}

		//Get Columns 
		var behavior = isFromMultiTables ? (CommandBehavior.SchemaOnly | CommandBehavior.SingleRow) : (CommandBehavior.KeyInfo | CommandBehavior.SingleRow);

		using (var command = connection.CreateCommand(sql))
		using (var reader = command.ExecuteReader(behavior))
		{
			do
			{
				var schema = reader.GetSchemaTable();
				foreach (DataRow row in schema.Rows)
				{
					var type = (Type)row["DataType"];
					var typeName = TypeAliases.ContainsKey(type) ? TypeAliases[type] : type.FullName;
					var allowDbNull = (bool)row["AllowDBNull"];
					var isNullable = allowDbNull && NullableTypes.Contains(type);
					var isKey = (bool)row["IsKey"];
					var isAutoIncrement = (bool)row["IsAutoIncrement"];
					var collumnName = (string)row["ColumnName"];
					var defaultValue = defaultColumns.FirstOrDefault(d => d.Item1 == collumnName)?.Item2;
					
					if (generatorBehavior.HasFlag(GeneratorBehavior.Comment) && !isFromMultiTables)
					{
						var comments = new[] { "DataTypeName", "IsUnique", "IsKey", "IsAutoIncrement", "IsReadOnly" }
							.Select(s =>
							{
								if (row[s] is bool && ((bool)row[s]))
									return s;
								if (row[s] is string && !string.IsNullOrWhiteSpace((string)row[s]))
									return string.Format(" {0} : {1} ", s, row[s]);
								return null;
							}).Where(w => w != null).ToArray();
						var sComment = string.Join(" , ", comments);

						builder.AppendFormat("		/// <summary>{0}</summary>{1}", sComment, Environment.NewLine);
					}

					bool key = false, explicitKey = false, computed = false;
					if (isKey && isAutoIncrement)
						key = true;
					else if (isKey && !isAutoIncrement)
						explicitKey = true;
					else if (!isKey && isAutoIncrement)
						computed = true;

					if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContrib) && !isFromMultiTables)
					{
						if (key)
							builder.AppendLine("		[Dapper.Contrib.Extensions.Key]");
						if (explicitKey)
							builder.AppendLine("		[Dapper.Contrib.Extensions.ExplicitKey]");
						if (computed)
							builder.AppendLine("		[Dapper.Contrib.Extensions.Computed]");
					}

					if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContribExtended) && !isFromMultiTables)
					{
						var uniqueEntry = uniqueColumns.FirstOrDefault(u => u.Item2 == collumnName);
						if (uniqueEntry != null)
						{
							if (uniqueIndexNames.Count > 1)
								builder.AppendLine($"		[UniqueConstraint({uniqueIndexNames.IndexOf(uniqueEntry.Item1) + 1})]");
							else
								builder.AppendLine("		[UniqueConstraint]");
						}
						
						if (!allowDbNull && !(key || computed) && string.IsNullOrEmpty(defaultValue))
							builder.AppendLine("		[System.ComponentModel.DataAnnotations.Required()]");

						if ((type == typeof(string) || type == typeof(DateTime)) && !string.IsNullOrEmpty(defaultValue) && !defaultValue.Contains('\''))
							builder.AppendLine($"		[System.ComponentModel.DefaultValue(null)]//Default Value: {defaultValue}");
						else if (!string.IsNullOrEmpty(defaultValue))
							builder.AppendLine($"		[System.ComponentModel.DefaultValue{defaultValue.Replace("'", "\"")}]");

						var foreignKey = foreignKeyColumns.FirstOrDefault(f => f.Item1 == collumnName);
						if (foreignKey != null)
						{
							var foreignKeyGroups = foreignKeyColumns
								.Where(x => x.Item2 == foreignKey.Item2 && x.Item3 == foreignKey.Item3)
								.OrderBy(x => x.Item4)
								.ToList();
							if (foreignKeyGroups.Count() > 1)
							{
								for (var batchNumber = 0; batchNumber < foreignKeyGroups.Count; batchNumber++)
								{
									if (foreignKey == foreignKeyGroups[batchNumber])
									{
										builder.AppendLine($"		[ForeignKey(typeof({foreignKey.Item2}), nameof({foreignKey.Item2}.{foreignKey.Item3}), {(batchNumber + 1)})]");
										break;
									}
								}
							}
							else
							{
								builder.AppendLine($"		[ForeignKey(typeof({foreignKey.Item2}), nameof({foreignKey.Item2}.{foreignKey.Item3}))]");
							}
						}
					}

					builder.AppendLine($"		public {typeName}{(isNullable ? "?" : string.Empty)} {collumnName} {{ get; set; }}");
					builder.AppendLine($"		");
				}

				var stringToTrim = "		" + Environment.NewLine;
				builder.Remove(builder.Length - stringToTrim.Length, stringToTrim.Length);

				builder.AppendLine("	}");
				builder.AppendLine();
			} while (reader.NextResult());

			return builder.ToString().TrimEnd();
		}
	}

	#region Private
	private static string[] Split(this string text, string splitText) => text.Split(new[] { splitText }, StringSplitOptions.None);
	private static IDbCommand CreateCommand(this IDbConnection connection, string sql)
	{
		var cmd = connection.CreateCommand();
		cmd.CommandText = sql;
		return cmd;
	}
	#endregion
}