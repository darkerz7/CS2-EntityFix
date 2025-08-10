using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2_ParseGravity;

internal static class ParseGravity
{
	internal static async Task Main(string[] args)
	{
		Console.Title = $"Parser Gravity for CS2-EntityFix";
		if (args.Length > 0)
		{
			if (File.Exists(args[0]))
			{
				await CreateJSONFromDictionary(GetGravityDictionary(args[0]));
				PrintToConsole("Completed!", ConsoleColor.Green);
			}
			else
			{
				PrintToConsole($"Error: File {args[0]} not found", ConsoleColor.Red);
			}
		}
		else
		{
			var pathcfg = Path.Combine(Environment.CurrentDirectory, $"ParseGravityConfig.json");

			if (File.Exists(pathcfg))
			{
				try
				{
					string sData = File.ReadAllText(pathcfg);
					g_cfg = JsonSerializer.Deserialize<JSONCFG>(sData);
				}
				catch (Exception e) { PrintToConsole($"Error: Deserialize ParseGravityConfig.json: {e}", ConsoleColor.Red); }
				if (g_cfg != null)
				{
					if (!string.IsNullOrEmpty(g_cfg.PathWorkshopFolger))
					{
						string sToFolger = Environment.CurrentDirectory;
						if (!string.IsNullOrEmpty(g_cfg.PathEntityFixFolger)) sToFolger = g_cfg.PathEntityFixFolger;

						if (Directory.Exists(g_cfg.PathWorkshopFolger))
						{
							if (Directory.Exists(sToFolger))
							{
								string[] sDirectories = Directory.GetDirectories(g_cfg.PathWorkshopFolger);
								foreach(var sDir in sDirectories)
								{
									DirectoryInfo dirinfo = new DirectoryInfo(sDir);
									PrintToConsole($"Opening workshopID: {dirinfo.Name}", ConsoleColor.Magenta);
									var path = $"{sDir}/{dirinfo.Name}.vpk";
									if (File.Exists(path))
									{
										PrintToConsole($"Gravity search for {path}", ConsoleColor.Blue);
										await CreateJSONFromDictionary(GetGravityDictionary(path), sToFolger);
									}
									else
									{
										path = $"{sDir}/{dirinfo.Name}_dir.vpk";
										if (File.Exists(path))
										{
											PrintToConsole($"Gravity search for {path}", ConsoleColor.Blue);
											await CreateJSONFromDictionary(GetGravityDictionary(path), sToFolger);
										}
									}
								}
								PrintToConsole("Completed!", ConsoleColor.Green);
							}
							else PrintToConsole($"Error: PathEntityFixFolger does not exist", ConsoleColor.Red);
						}
						else PrintToConsole($"Error: PathWorkshopFolger does not exist", ConsoleColor.Red);
					}
					else PrintToConsole($"Error: PathWorkshopFolger is empty", ConsoleColor.Red);
				}
				else PrintToConsole($"Error: File ParseGravityConfig.json is bad", ConsoleColor.Red);
			}
			else PrintToConsole($"Error: File ParseGravityConfig.json not found", ConsoleColor.Red);
		}
		PrintToConsole($"Press any key", ConsoleColor.Green);
		Console.ReadKey();
	}

	static JSONCFG? g_cfg = new JSONCFG();

	class JSONCFG
	{
		public string PathWorkshopFolger { get; set; }
		public string PathEntityFixFolger { get; set; }
		public JSONCFG()
		{
			PathWorkshopFolger = "";
			PathEntityFixFolger = "";
		}
	}

	static Dictionary<string, Dictionary<string, float>> GetGravityDictionary(string sFile)
	{
		Dictionary<string, Dictionary<string, List<Entity>>> Lump = ParseEntities.ParseByFileName(sFile);
		Dictionary<string, Dictionary<string, float>> Return = new Dictionary<string, Dictionary<string, float>>();
		foreach (var Entry in Lump)
		{
			//Console.WriteLine($"Entry: {Entry.Key}");
			Dictionary<string, float> GravityHIDValue = new Dictionary<string, float>();
			foreach (var file in Entry.Value)
			{
				//Console.WriteLine($"File: {file.Key}");
				foreach (var entity in file.Value)
				{
					//Console.WriteLine($"ClassName: {entity.Classname} TargetName: {entity.Targetname} HammerID: {entity.HammerUniqueId}");
					if (entity.Classname.Equals("trigger_gravity"))
					{
						//Console.WriteLine($"Entry: {Entry.Key} File: {file.Key} HammerID: {entity.HammerUniqueId}");
						float flGravity = 0.01f;
						float.TryParse(entity.Properties.SingleOrDefault(x => x.Key.Equals("gravity", StringComparison.OrdinalIgnoreCase)).Value?.ToString(), out flGravity);
						GravityHIDValue.Add(entity.HammerUniqueId, flGravity);
					}
				}
			}
			if (GravityHIDValue.Count > 0)
			{
				/*foreach (var entity in GravityHIDValue)
				{
					Console.WriteLine($"Entry: {Entry.Key} HammerID: {entity.Key} Value: {entity.Value}");
				}*/
				PrintToConsole($"{GravityHIDValue.Count} trigger_gravity found in {Entry.Key}", ConsoleColor.Yellow);
				Return.Add(Entry.Key, GravityHIDValue);
			}
		}
		return Return;
	}

	static async Task CreateJSONFromDictionary(Dictionary<string, Dictionary<string, float>> GravityToJSON, string? sOutputPath = null)
	{
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};
		foreach (var value in GravityToJSON)
		{
			{
				var text = JsonSerializer.Serialize(value.Value, options);
				var path = Path.Combine(string.IsNullOrEmpty(sOutputPath) ? Environment.CurrentDirectory : sOutputPath, $"{value.Key}.json");
				await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
			}
		}
	}

	static void PrintToConsole(string sMessage, ConsoleColor color)
	{
		Console.ForegroundColor = color;
		Console.WriteLine(sMessage);
		Console.ResetColor();
	}
}