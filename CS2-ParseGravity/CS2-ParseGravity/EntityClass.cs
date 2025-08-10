using SteamDatabase.ValvePak;
using System.Globalization;
using ValveResourceFormat;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.Serialization.KeyValues;
using KVValueType = ValveKeyValue.KVValueType;

namespace CS2_ParseGravity
{
	static class ParseEntities
	{
		public static Dictionary<string, Dictionary<string, List<Entity>>> ParseByFileName(string file)
		{
			var lump = new Dictionary<string, Dictionary<string, List<Entity>>>();
			try
			{
				using var vpk = new Package();
				vpk.Read(file);
				foreach (var (name, entries) in vpk.Entries)
				{
					if (name.Equals("vpk"))
					{
						foreach (var entry in entries)
						{
							vpk.ReadEntry(entry, out var bin);
							lump.Add(entry.FileName, ParseMapVpk(bin));
						}
					}
				}
				
			}
			catch (Exception) { }
			return lump;
		}
		private static Dictionary<string, List<Entity>> ParseMapVpk(byte[] bin)
		{
			using var memory = new MemoryStream(bin);

			using var vpk = new Package();
			vpk.SetFileName("map.vpk");
			vpk.Read(memory);

			var entities = new Dictionary<string, List<Entity>>();

			foreach (var (name, entries) in vpk.Entries)
			{
				if (name.Equals("vents_c"))
				{
					foreach (var vent in entries)
					{
						try
						{
							vpk.ReadEntry(vent, out var ventBytes);

							var entityBlock = ParseLump(vent.FileName, vent.TypeName, ventBytes);
							entities.Add(vent.FileName, [.. entityBlock]);
						}
						catch (Exception) { }
					}
				}
			}
			return entities;
		}
		private static IEnumerable<Entity> ParseLump(string vent, string ext, byte[] ventBytes)
		{
			using var memory = new MemoryStream(ventBytes);
			using var resource = new Resource();
			resource.FileName = $"{vent}.{ext}";
			resource.Read(memory);

			if (resource.DataBlock is EntityLump { } lump)
			{
				foreach (var entity in lump.GetEntities())
				{
					yield return new Entity(entity);
				}
			}
		}
	}
	internal record Entity
	{
		internal record Connection(string Output, string Target, string Input, string Param, double Delay, long TimeToUse);
		public string Classname { get; }
		public string HammerUniqueId { get; }
		public string? Targetname { get; }
		public Dictionary<string, object> Properties { get; }
		public List<Connection>? Connections { get; }

		public Entity(EntityLump.Entity entity)
		{
			Classname = GetEntityRequiredProperty(entity, "classname");
			HammerUniqueId = GetEntityRequiredProperty(entity, "hammerUniqueId");
			Targetname = GetEntityProperty(entity, "targetname");

			Properties = [];

			foreach (var (key, value) in entity.Properties)
			{
				if (key.Equals("classname", StringComparison.OrdinalIgnoreCase)
					|| key.Contains("targetname", StringComparison.OrdinalIgnoreCase)
					|| key.Contains("hammerUniqueId", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (value is KVObject kvObject)
				{
					if (kvObject.IsArray)
					{
						var values = string.Join(' ', kvObject.Properties.Select(x => KVValueString(x.Value)));
						Properties.Add(key, values);

						Properties.Add($"{key} __RAW_ARRAY__ ({kvObject.Properties.First().Value.Type}[{kvObject.Count}])",
									   kvObject.Properties
											   .Select(x => x.Value.Value)
											   .ToArray());
					}
					else
					{
						Properties.Add($"{key} __RAW_OBJECT__ (Pair)",
									   kvObject.Properties.ToDictionary(x => x.Key, x => KVValueString(x.Value)));
					}

					continue;
				}

				Properties.Add(key, value.ToString() ?? "<_null_>");
			}

			if (entity.Connections is not null)
			{
				Connections = [];

				foreach (var connection in entity.Connections)
				{
					long timesToFire;

					try
					{
						timesToFire = connection.GetProperty<long>("m_nTimesToFire");
					}
					catch
					{
						timesToFire = connection.GetProperty<int>("m_nTimesToFire");
					}

					var io = new Connection(connection.GetProperty<string>("m_outputName"),
											connection.GetProperty<string>("m_targetName"),
											connection.GetProperty<string>("m_inputName"),
											connection.GetProperty<string>("m_overrideParam"),
											Math.Round(connection.GetProperty<double>("m_flDelay"), 4),
											timesToFire);

					Connections.Add(io);
				}
			}
		}
		public static string GetEntityProperty(EntityLump.Entity entity, string property, string defaultVal)
		=> entity.Properties
				 .SingleOrDefault(x => x.Key.Equals(property, StringComparison.OrdinalIgnoreCase))
				 .Value?.ToString()
		   ?? defaultVal;

		public static string? GetEntityProperty(EntityLump.Entity entity, string property)
			=> entity.Properties
					 .SingleOrDefault(x => x.Key.Equals(property, StringComparison.OrdinalIgnoreCase))
					 .Value?.ToString();

		public static string GetEntityRequiredProperty(EntityLump.Entity entity, string property)
			=> entity.Properties
					 .SingleOrDefault(x => x.Key.Equals(property, StringComparison.OrdinalIgnoreCase))
					 .Value?.ToString()
			   ?? throw new KeyNotFoundException($"{property} not found");
		private static object? KVValueString(KVValue value)
			=> value.Type switch
			{
				KVValueType.Collection or KVValueType.Array => value.Value != null ? ((KVObject)value.Value).Properties : null,
				KVValueType.String => value.Value != null ? (string)value.Value : null,
				KVValueType.Boolean => value.Value != null ? ((bool)value.Value).ToString()
														   .ToLower() : false,
				KVValueType.FloatingPoint => Convert.ToSingle(value.Value, CultureInfo.InvariantCulture)
													.ToString("#0.000000", CultureInfo.InvariantCulture),
				KVValueType.FloatingPoint64 => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)
													  .ToString("#0.000000", CultureInfo.InvariantCulture),
				KVValueType.Int64 => Convert.ToInt64(value.Value, CultureInfo.InvariantCulture),
				KVValueType.UInt64 => Convert.ToUInt64(value.Value, CultureInfo.InvariantCulture),
				KVValueType.Int32 => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture),
				KVValueType.UInt32 => Convert.ToUInt32(value.Value, CultureInfo.InvariantCulture),
				KVValueType.Int16 => Convert.ToInt16(value.Value, CultureInfo.InvariantCulture),
				KVValueType.UInt16 => Convert.ToUInt16(value.Value, CultureInfo.InvariantCulture),
				KVValueType.Null => "<_null_>",
				_ => throw new NotSupportedException($"Not Support type {value.Type}"),
			};
	}
}
