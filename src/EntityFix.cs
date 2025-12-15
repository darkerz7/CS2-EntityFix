using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Runtime.CompilerServices;
using System.Text.Json;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2_EntityFix
{
	[MinimumApiVersion(330)]
	
	public class CInputData(IntPtr pointer) : NativeObject(pointer)
	{
		public unsafe ref nint PActivator => ref Unsafe.AsRef<nint>((void*)Handle);
		public unsafe ref nint PCaller => ref Unsafe.AsRef<nint>((void*)(Handle + 8));
		public unsafe ref nint PValue => ref Unsafe.AsRef<nint>((void*)(Handle + 16));
		public unsafe ref int NOutputID => ref Unsafe.AsRef<int>((void*)(Handle + 24));
	}
	public class CGameUI(CLogicCase gameUI)
	{
		public CEntityInstance? cActivator = null;
		public CLogicCase GameUI = gameUI;
		//public PlayerButtons LastButtonState;
	}
	public class CIgnite(CEntityInstance? cEnt, CParticleSystem? cPart, double fEnds, CounterStrikeSharp.API.Modules.Timers.Timer? tim)
	{
		public CEntityInstance? cEntity = cEnt;
		public CParticleSystem? cParticle = cPart;
		public double fEnd = fEnds;
		public CounterStrikeSharp.API.Modules.Timers.Timer? timer = tim;
	}
	public class CViewControl
	{
		public CLogicRelay ViewControl;
		public CEntityInstance? Target;
		public List<CCSPlayerController> Players;
		public CViewControl(CLogicRelay Entity)
		{
			ViewControl = Entity;
			Players = [];
			var ents = Utilities.GetAllEntities();
			foreach (var ent in ents.ToList())
			{
				if (ent != null && ent.IsValid && ent.Entity != null && string.Compare(ent.Entity.Name, ViewControl.Target) == 0)
				{
					Target = ent;
					break;
				}
			}
		}
		public void EnableCamera(CCSPlayerController Activator)
		{
			Players.Add(Activator);
			UpdateState(Activator, true);
		}
		public void DisableCamera(CCSPlayerController Activator)
		{
			Players.Remove(Activator);
			UpdateState(Activator, false);
		}
		public void EnableCameraAll()
		{
			Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(pl =>
			{
				EnableCamera(pl);
			});
		}
		public void DisableCameraAll()
		{
			Players.Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(pl =>
			{
				DisableCamera(pl);
			});
			Players.Clear();
		}
		public void UpdateState(CCSPlayerController cActivator, bool bEnable)
		{
			if (cActivator != null && cActivator.IsValid && Target != null && Target.IsValid)
			{
				if (cActivator.PlayerPawn.Value != null && cActivator.PlayerPawn.Value.IsValid && cActivator.PlayerPawn.Value.CameraServices != null)
				{
					if (bEnable) cActivator.PlayerPawn.Value.CameraServices.ViewEntity.Raw = Target.EntityHandle.Raw;
					else cActivator.PlayerPawn.Value.CameraServices.ViewEntity.Raw = uint.MaxValue;
					Utilities.SetStateChanged(cActivator.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");
				}
				if ((ViewControl.Spawnflags & 64) != 0) // FOV
				{
					if (bEnable && (uint)ViewControl.Health >= 16 && (uint)ViewControl.Health <= 179)
					{
						cActivator.DesiredFOV = (uint)ViewControl.Health;
					}
					else cActivator.DesiredFOV = 90;
					Utilities.SetStateChanged(cActivator, "CBasePlayerController", "m_iDesiredFOV");
				}
				if ((ViewControl.Spawnflags & 32) != 0 && cActivator.PlayerPawn.Value != null) // Freeze
				{
					if (bEnable) cActivator.PlayerPawn.Value.Flags |= (uint)PlayerFlags.FL_FROZEN;
					else cActivator.PlayerPawn.Value.Flags &= ~(uint)PlayerFlags.FL_FROZEN;
				}
				if ((ViewControl.Spawnflags & 128) != 0) // Disarm
				{
					if (bEnable && cActivator.PlayerPawn.Value != null && cActivator.PlayerPawn.Value.IsValid && cActivator.PlayerPawn.Value.WeaponServices != null)
					{
						var activeweapon = cActivator.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value;
						if (activeweapon != null && activeweapon.IsValid)
						{
							activeweapon.NextPrimaryAttackTick = Math.Max(activeweapon.NextPrimaryAttackTick, Server.TickCount + 24);
							Utilities.SetStateChanged(activeweapon, "CBasePlayerWeapon", "m_nNextPrimaryAttackTick");
							activeweapon.NextSecondaryAttackTick = Math.Max(activeweapon.NextSecondaryAttackTick, Server.TickCount + 24);
							Utilities.SetStateChanged(activeweapon, "CBasePlayerWeapon", "m_nNextSecondaryAttackTick");
						}
					}
				}
			}
		}
	}
	[Flags]
	public enum EquipFlags : uint
	{
		SF_PLAYEREQUIP_NONE = 0,
		SF_PLAYEREQUIP_USEONLY = 1,
		SF_PLAYEREQUIP_STRIPFIRST = 2,
		SF_PLAYEREQUIP_ONLYSTRIPSAME = 4,
	}
	public class EntityFix : BasePlugin
	{
		readonly static MemoryFunctionVoid<CEntityIdentity, CUtlSymbolLarge, CEntityInstance, CEntityInstance, CVariant, int> CEntityIdentity_AcceptInputFunc = new(GameData.GetSignature("CEntityIdentity_AcceptInput"));
		readonly static MemoryFunctionVoid<CBaseEntity, CInputData> CBaseFilter_InputTestActivatorFunc = new(GameData.GetSignature("CBaseFilter_InputTestActivator"));
		readonly static MemoryFunctionVoid<CBaseEntity, CBaseEntity> CTriggerGravity_GravityTouchFunc = new(GameData.GetSignature("CTriggerGravity_GravityTouch"));
		readonly static MemoryFunctionVoid<CBaseEntity, float> CBaseEntity_SetGravityScaleFunc = new(GameData.GetSignature("CBaseEntity_SetGravityScale"));
		readonly static Action<CBaseEntity, float> SetGravityScale = CBaseEntity_SetGravityScaleFunc.Invoke;
		readonly List<CGameUI> g_GameUI = [];
		readonly List<CIgnite> g_Ignite = [];
		readonly List<CViewControl> g_ViewControl = [];
		ConfigJSON? cfg = new();
		Dictionary<string, float>? g_GravityCFG;
		float g_VelocityIgnite = 0.45f;
		float g_RepeatIgnite = 0.5f;
		int g_DamageIgnite = 1;
		string g_PathIgnite = "particles/burning_fx/env_fire_small.vpcf";
		public override string ModuleName => "Entity Fix";
		public override string ModuleDescription => "Fixes game_player_equip, game_ui, point_viewcontrol, IgniteLifeTime";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.15";
		public override void Load(bool hotReload)
		{
			LoadCFG();
			RegisterListener<OnServerPrecacheResources>(OnPrecacheResources);
			RegisterListener<OnMapStart>(OnMapStart_Listener);
			CEntityIdentity_AcceptInputFunc.Hook(OnInput, HookMode.Pre);
			CBaseFilter_InputTestActivatorFunc.Hook(OnInputTestActivator, HookMode.Pre);
			CTriggerGravity_GravityTouchFunc.Hook(OnGravityTouch, HookMode.Pre);
			HookEntityOutput("trigger_gravity", "OnEndTouch", (output, name, activator, caller, value, delay) =>
			{
				var player = EntityIsPlayer(activator);
				if (player != null && player.PlayerPawn.Value != null)
				{
					SetGravityScale(player.PlayerPawn.Value, 1.0f);
					return HookResult.Handled;
				}
				return HookResult.Continue;
			});
			RegisterListener<OnEntitySpawned>(OnEntitySpawned_Listener);
			RegisterListener<OnEntityDeleted>(OnEntityDeleted_Listener);
			RegisterListener<OnTick>(OnOnTick_Listener);
			RegisterListener<OnPlayerButtonsChanged>(OnOnPlayerButtonsChanged_Listener);
			RegisterEventHandler<EventRoundStart>(OnEventRoundStart);
			RegisterEventHandler<EventRoundEnd>(OnEventRoundEnd);
			RegisterEventHandler<EventPlayerDeath>(OnEventPlayerDeathPost);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
		}
		public override void Unload(bool hotReload)
		{
			RemoveCommand("css_entityfix_reload", OnReload);
			RemoveListener<OnServerPrecacheResources>(OnPrecacheResources);
			RemoveListener<OnMapStart>(OnMapStart_Listener);
			CEntityIdentity_AcceptInputFunc.Unhook(OnInput, HookMode.Pre);
			CBaseFilter_InputTestActivatorFunc.Unhook(OnInputTestActivator, HookMode.Pre);
			CTriggerGravity_GravityTouchFunc.Unhook(OnGravityTouch, HookMode.Pre);
			RemoveListener<OnEntitySpawned>(OnEntitySpawned_Listener);
			RemoveListener<OnEntityDeleted>(OnEntityDeleted_Listener);
			RemoveListener<OnTick>(OnOnTick_Listener);
			RemoveListener<OnPlayerButtonsChanged>(OnOnPlayerButtonsChanged_Listener);
			DeregisterEventHandler<EventRoundStart>(OnEventRoundStart);
			DeregisterEventHandler<EventRoundEnd>(OnEventRoundEnd);
			DeregisterEventHandler<EventPlayerDeath>(OnEventPlayerDeathPost);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
		}
		void LoadCFG()
		{
			string sConfig = $"{Path.Join(ModuleDirectory, "config.json")}";
			string sData;
			if (File.Exists(sConfig))
			{
				try
				{
					sData = File.ReadAllText(sConfig);
					cfg = JsonSerializer.Deserialize<ConfigJSON>(sData);

					if (cfg != null)
					{
						if (cfg.Ignite_Velocity >= 0.001f && cfg.Ignite_Velocity <= 1.0f) g_VelocityIgnite = cfg.Ignite_Velocity;
						else g_VelocityIgnite = 0.45f;

						if (cfg.Ignite_Repeat >= 0.1f && cfg.Ignite_Repeat <= 1.0f) g_RepeatIgnite = cfg.Ignite_Repeat;
						else g_RepeatIgnite = 0.5f;

						if (cfg.Ignite_Damage >= 1 && cfg.Ignite_Damage <= 1000) g_DamageIgnite = cfg.Ignite_Damage;
						else g_DamageIgnite = 1;

						if (!string.IsNullOrEmpty(cfg.Ignite_Particle)) g_PathIgnite = cfg.Ignite_Particle.Replace("\"", "");
						else g_PathIgnite = "particles/burning_fx/env_fire_small.vpcf";
					}
				}
				catch
				{
					cfg = null;
					PrintToConsole($"Bad Config file ({sConfig})");
				}
			}
			else
			{
				cfg = null;
				PrintToConsole($"Config file ({sConfig}) not found");
			}
		}
		class ConfigJSON
		{
			public float Ignite_Velocity { get; set; }
			public float Ignite_Repeat { get; set; }
			public int Ignite_Damage { get; set; }
			public string? Ignite_Particle { get; set; }
		}
		[ConsoleCommand("css_entityfix_reload", "Reload config file of EntityFix")]
		[RequiresPermissions("@css/root")]
		public void OnReload(CCSPlayerController? player, CommandInfo command)
		{
			if (player != null && !player.IsValid) return;
			LoadCFG();
			if (cfg != null)
			{
				if (player != null)
				{
					command.ReplyToCommand(" \x0B[\x04 EntityFix \x0B]\x01 ConfigFile reloaded!");
					PrintToConsole($"ConfigFile reloaded by {player.PlayerName} ({player.SteamID})");
				}
				else PrintToConsole($"ConfigFile reloaded!");
			}
		}
		private void OnPrecacheResources(ResourceManifest manifest)
		{
			manifest.AddResource(g_PathIgnite);
		}
		private void OnMapStart_Listener(string sMapName)
		{
			g_GravityCFG?.Clear();
			string sConfig = $"{Path.Join(ModuleDirectory, $"maps/{sMapName}.json")}";
			if (File.Exists(sConfig))
			{
				try
				{
					string sData = File.ReadAllText(sConfig);
					g_GravityCFG = JsonSerializer.Deserialize<Dictionary<string, float>>(sData);
				}
				catch { g_GravityCFG = null;}
				PrintToConsole($"Loaded GravityFix from {sConfig}");
			}
			else g_GravityCFG = null;
		}
		private void OnEntitySpawned_Listener(CEntityInstance entity)
		{
			if (IsGameUI(entity))
			{
				CGameUI gameui = new(new CLogicCase(entity.Handle));
				g_GameUI.Add(gameui);
			}
			else if (IsViewControl(entity))
			{
				CViewControl viewcontrol = new(new CLogicRelay(entity.Handle));
				viewcontrol.ViewControl.Disabled = false; //help mappers identify server fix
				g_ViewControl.Add(viewcontrol);
			}
		}
		private void OnEntityDeleted_Listener(CEntityInstance entity)
		{
			if (IsGameUI(entity))
			{
				CLogicCase gameui = new(entity.Handle);
				if ((gameui.Spawnflags & 32) == 0)
				{
					foreach (var GTest in g_GameUI.ToList())
					{
						if (GTest.GameUI == gameui)
						{
							Server.NextWorldUpdate(() =>
							{
								if (GTest.cActivator != null)
								{
									GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
								}
								Server.NextWorldUpdate(() =>
								{
									g_GameUI.Remove(GTest);
								});
							});
						}
					}
				}
			}
			else if (IsViewControl(entity))
			{
				CLogicRelay relay = new(entity.Handle);
				foreach (var vc in g_ViewControl.ToList())
				{
					if (vc.ViewControl == relay)
					{
						if (vc.Target != null && vc.Target.IsValid) vc.DisableCameraAll();
						Server.NextWorldUpdate(() =>
						{
							g_ViewControl.Remove(vc);
						});
						break;
					}
				}
			}
			else if (IsTarget(entity))
			{
				foreach (var vc in g_ViewControl.ToList())
				{
					if (vc.Target == entity)
					{
						vc.DisableCameraAll();
						break;
					}
				}
			}
		}
		private void OnOnPlayerButtonsChanged_Listener(CCSPlayerController player, PlayerButtons pressed, PlayerButtons released)
		{
			foreach (var GTest in g_GameUI.ToList())
			{
				var plTest = EntityIsPlayer(GTest.cActivator);
				if (plTest == null) continue;
				if (plTest == player)
				{
					if ((GTest.GameUI.Spawnflags & 256) != 0 && (pressed & PlayerButtons.Jump) != 0)
					{
						GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
						continue;
					}

					if ((pressed & PlayerButtons.Forward) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedForward");
					if ((released & PlayerButtons.Forward) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedForward");

					if ((pressed & PlayerButtons.Moveleft) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedMoveLeft");
					if ((released & PlayerButtons.Moveleft) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedMoveLeft");

					if ((pressed & PlayerButtons.Back) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedBack");
					if ((released & PlayerButtons.Back) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedBack");

					if ((pressed & PlayerButtons.Moveright) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedMoveRight");
					if ((released & PlayerButtons.Moveright) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedMoveRight");

					if ((pressed & PlayerButtons.Attack) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedAttack");
					if ((released & PlayerButtons.Attack) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedAttack");

					if ((pressed & PlayerButtons.Attack2) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedAttack2");
					if ((released & PlayerButtons.Attack2) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedAttack2");

					if ((pressed & PlayerButtons.Speed) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedSpeed");
					if ((released & PlayerButtons.Speed) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedSpeed");

					if ((pressed & PlayerButtons.Duck) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedDuck");
					if ((released & PlayerButtons.Duck) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedDuck");

					if ((pressed & PlayerButtons.Use) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedUse");
					if ((released & PlayerButtons.Use) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedUse");

					if ((pressed & PlayerButtons.Reload) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedReload");
					if ((released & PlayerButtons.Reload) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedReload");

					if ((pressed & PlayerButtons.Inspect) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "PressedLook");
					if ((released & PlayerButtons.Inspect) != 0) GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, "UnpressedLook");
				}
			}
		}

		private void OnOnTick_Listener()
		{
			/*foreach (var GTest in g_GameUI.ToList())
			{
				if (GTest.cActivator != null)
				{
					var player = EntityIsPlayer(GTest.cActivator);
					if (player == null) continue;
					if((GTest.GameUI.Spawnflags & 256) != 0 && (player.Buttons & PlayerButtons.Jump) != 0)
					{
						GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
						continue;
					}

					PlayerButtons buttonChanged =  player.Buttons ^ GTest.LastButtonState;

					if ((buttonChanged & PlayerButtons.Forward) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Forward) != 0 ? "UnpressedForward" : "PressedForward");
					}
					if ((buttonChanged & PlayerButtons.Moveleft) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Moveleft) != 0 ? "UnpressedMoveLeft" : "PressedMoveLeft");
					}
					if ((buttonChanged & PlayerButtons.Back) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Back) != 0 ? "UnpressedBack" : "PressedBack");
					}
					if ((buttonChanged & PlayerButtons.Moveright) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Moveright) != 0 ? "UnpressedMoveRight" : "PressedMoveRight");
					}
					if ((buttonChanged & PlayerButtons.Attack) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Attack) != 0 ? "UnpressedAttack" : "PressedAttack");
					}
					if ((buttonChanged & PlayerButtons.Attack2) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Attack2) != 0 ? "UnpressedAttack2" : "PressedAttack2");
					}
					if ((buttonChanged & PlayerButtons.Speed) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Speed) != 0 ? "UnpressedSpeed" : "PressedSpeed");
					}
					if ((buttonChanged & PlayerButtons.Duck) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Duck) != 0 ? "UnpressedDuck" : "PressedDuck");
					}
					if ((buttonChanged & PlayerButtons.Use) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Use) != 0 ? "UnpressedUse" : "PressedUse");
					}
					if ((buttonChanged & PlayerButtons.Reload) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Reload) != 0 ? "UnpressedReload" : "PressedReload");
					}
					if ((buttonChanged & PlayerButtons.Inspect) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & PlayerButtons.Inspect) != 0 ? "UnpressedLook" : "PressedLook");
					}

					GTest.LastButtonState = player.Buttons;
				}
			}*/
			foreach (var vc in g_ViewControl.ToList())
			{
				vc.Players.Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(pl =>
				{
					vc.UpdateState(pl, true);
				});
			}
		}
		private HookResult OnEventRoundStart(EventRoundStart @event, GameEventInfo info)
		{
			foreach (var IgniteCheck in g_Ignite.ToList()) IgniteClear(IgniteCheck);
			Server.NextWorldUpdate(g_Ignite.Clear);
			return HookResult.Continue;
		}
		private HookResult OnEventRoundEnd(EventRoundEnd @event, GameEventInfo info)
		{
			g_GameUI.Clear();
			g_ViewControl.Clear();
			foreach (var IgniteCheck in g_Ignite.ToList()) IgniteClear(IgniteCheck);
			Server.NextWorldUpdate(g_Ignite.Clear);
			return HookResult.Continue;
		}
		[GameEventHandler(mode: HookMode.Post)]
		private HookResult OnEventPlayerDeathPost(EventPlayerDeath @event, GameEventInfo info)
		{
			OnGameUIEventDeactivate(@event.Userid);
			return HookResult.Continue;
		}
		[GameEventHandler]
		private HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			OnGameUIEventDeactivate(@event.Userid);
			if (@event.Userid != null && @event.Userid.IsValid)
			{
				foreach (var vc in g_ViewControl.ToList())
				{
					if (vc.Target != null && vc.Target.IsValid) vc.DisableCamera(@event.Userid);
				}
			}
			return HookResult.Continue;
		}
		private HookResult OnInputTestActivator(DynamicHook hook)
		{
			//Console.WriteLine($"[EntityFix-Test]: Activator: {hook.GetParam<CInputData>(1).Activator?.DesignerName}");
			if (hook.GetParam<CInputData>(1).PActivator == IntPtr.Zero) return HookResult.Handled;

			return HookResult.Continue;
		}
		private HookResult OnGravityTouch(DynamicHook hook)
		{
			var player = EntityIsPlayer(hook.GetParam<CBaseEntity>(1));
			if (player != null && player.PlayerPawn.Value != null && IsPlayerAlive(player))
			{
				float flValue = 0.01f;
				if (g_GravityCFG != null && g_GravityCFG.Count > 0)
				{
					var gravity = hook.GetParam<CBaseEntity>(0);
					if (!string.IsNullOrEmpty(gravity.UniqueHammerID) && g_GravityCFG.TryGetValue(gravity.UniqueHammerID, out float value)) // Need CS2-HammerIDFix
					{
						flValue = value;
					}
				}
				SetGravityScale(player.PlayerPawn.Value, flValue);
				return HookResult.Handled;
			}
			return HookResult.Continue;
		}
		private HookResult OnInput(DynamicHook hook)
		{
			var cEntity = hook.GetParam<CEntityIdentity>(0);
			var sInput = hook.GetParam<CUtlSymbolLarge>(1).String;
			if (string.IsNullOrEmpty(sInput)) return HookResult.Continue;
			var cActivator = hook.GetParam<CEntityInstance>(2);
			var cCaller = hook.GetParam<CEntityInstance>(3);
			var cValue = hook.GetParam<CVariant>(4);
			var sValue = cValue.FieldType == fieldtype_t.FIELD_CSTRING ? NativeAPI.GetStringFromSymbolLarge(cValue.Handle) : "";
			if (sInput.Contains("ignitel", StringComparison.OrdinalIgnoreCase))
			{
				if (float.TryParse(sValue, out float fDuration))
				{
					IgnitePawn(cActivator, fDuration);
				}
			} else if (string.Equals(cEntity.DesignerName, "game_player_equip"))
			{
				var ent = new CGamePlayerEquip(cEntity.EntityInstance.Handle);
				if (((EquipFlags)ent.Spawnflags).HasFlag(EquipFlags.SF_PLAYEREQUIP_STRIPFIRST))
				{
					if (string.Equals(sInput.ToLower(), "use") || string.Equals(sInput.ToLower(), "triggerforactivatedplayer"))
					{
						CCSPlayerController? cPlayer = EntityIsPlayer(cActivator);
						if (cPlayer != null && IsPlayerAlive(cPlayer))
						{
							cPlayer.RemoveWeapons();
							if (string.Equals(sInput.ToLower(), "triggerforactivatedplayer") && !string.IsNullOrEmpty(sValue)) cPlayer.GiveNamedItem(sValue);
						}
					}else if(string.Equals(sInput.ToLower(), "triggerforallplayers"))
					{
						Utilities.GetPlayers().Where(p => p is { IsValid: true, IsHLTV: false }).ToList().ForEach(pl =>
						{
							if (IsPlayerAlive(pl)) pl.RemoveWeapons();
						});
					}
				}
			} else if(IsGameUI(new CEntityInstance(cEntity.EntityInstance.Handle)))
			{
				if (string.Equals(sInput.ToLower(), "activate")) OnGameUI(cActivator, new CLogicCase(cEntity.EntityInstance.Handle), true);
				else if (string.Equals(sInput.ToLower(), "deactivate")) OnGameUI(cActivator, new CLogicCase(cEntity.EntityInstance.Handle), false);
			} else if (IsViewControl(new CEntityInstance(cEntity.EntityInstance.Handle)))
			{
				CLogicRelay relay = new(cEntity.EntityInstance.Handle);
				foreach (var vc in g_ViewControl.ToList())
				{
					if (vc.ViewControl == relay)
					{
						if (vc.Target != null && vc.Target.IsValid)
						{
							switch (sInput.ToLower())
							{
								case "enablecamera":
									CCSPlayerController? cPlayerEC = EntityIsPlayer(cActivator);
									if (cPlayerEC != null) vc.EnableCamera(cPlayerEC);
									break;
								case "disablecamera":
									CCSPlayerController? cPlayerDC = EntityIsPlayer(cActivator);
									if (cPlayerDC != null) vc.DisableCamera(cPlayerDC);
									break;
								case "enablecameraall": vc.EnableCameraAll(); break;
								case "disablecameraall": vc.DisableCameraAll();  break;
							}
						}
						break;
					}
				}
			}
			return HookResult.Continue;
		}
		void IgnitePawn(CEntityInstance cActivator, float fDuration)
		{
			if (cActivator == null || !cActivator.IsValid) return;
			foreach (var IgniteCheck in g_Ignite.ToList())
			{
				if(IgniteCheck.cEntity == cActivator && IgniteCheck.timer != null)
				{
					double fNewTime = Server.EngineTime + fDuration;
					if (fNewTime > IgniteCheck.fEnd) IgniteCheck.fEnd = fNewTime;
					return;
				}
			}
			
			CEntityInstance cActivatorBuf = new(cActivator.Handle);
			CBaseEntity pawn = new(cActivatorBuf.Handle);
			CParticleSystem? particle = null;
			if (pawn != null && pawn.IsValid)
			{
				particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
				if (particle != null && particle.IsValid)
				{
					particle.EffectName = g_PathIgnite;
					particle.TintCP = 1;
					particle.Tint = System.Drawing.Color.FromArgb(255, 255, 0, 0);
					particle.StartActive = true;

					particle.Teleport(pawn.AbsOrigin, pawn.AbsRotation, pawn.AbsVelocity);
					particle.DispatchSpawn();
				}
			}
			CIgnite cNewIgnite = new(cActivatorBuf, particle, Server.EngineTime + fDuration, new CounterStrikeSharp.API.Modules.Timers.Timer(g_RepeatIgnite, () =>
			{
				try
				{
					foreach (var IgniteCheck in g_Ignite.ToList())
					{
						if (IgniteCheck.cEntity == cActivatorBuf)
						{
							
							if (Server.EngineTime < IgniteCheck.fEnd && IgniteCheck.cEntity != null && IgniteCheck.cEntity.IsValid)
							{
								if (IgniteCheck.cParticle != null && IgniteCheck.cParticle.IsValid)
								{
									CBaseEntity pawn = new(IgniteCheck.cEntity.Handle);
									IgniteCheck.cParticle.Teleport(pawn.AbsOrigin, pawn.AbsRotation, pawn.AbsVelocity);
								}
								var player = EntityIsPlayer(cActivatorBuf);
								if (player != null && player.PlayerPawn.Value != null)
								{
									player.PlayerPawn.Value.VelocityModifier *= g_VelocityIgnite;
									player.PlayerPawn.Value.Health -= g_DamageIgnite;
									Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
									if (player.PlayerPawn.Value.Health <= 0) player.PlayerPawn.Value.CommitSuicide(true, true);
								}
								//Console.WriteLine($"Ignite: {cActivatorBuf.Index}. Left:{IgniteCheck.fEnd - Server.EngineTime}");
							} else
							{
								IgniteClear(IgniteCheck);
								Server.NextWorldUpdate(() => g_Ignite.Remove(IgniteCheck));
							}
						}
					}
				}
				catch (Exception) { }
			}, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT));
			g_Ignite.Add(cNewIgnite);
		}

		static void IgniteClear(CIgnite IgniteCheck)
		{
			if (IgniteCheck.timer != null)
			{
				IgniteCheck.timer.Kill();
				IgniteCheck.timer = null;
			}
			if (IgniteCheck.cParticle != null && IgniteCheck.cParticle.IsValid)
			{
				IgniteCheck.cParticle.AcceptInput("Stop");
				IgniteCheck.cParticle.Remove();
			}
			var player = EntityIsPlayer(IgniteCheck.cEntity);
			if (player != null && player.PlayerPawn.Value != null)
			{
				player.PlayerPawn.Value.VelocityModifier = 1.0f;
			}
		}
		void OnGameUI(CEntityInstance cActivator, CLogicCase cGameUI, bool bActivate)
		{
			if (cActivator == null || !cActivator.IsValid) return;
			if ((cGameUI.Spawnflags & 32) != 0)
			{
				CCSPlayerController? cPlayer = EntityIsPlayer(cActivator);
				if (cPlayer != null && cPlayer.PlayerPawn.Value != null)
				{
					if (bActivate) cPlayer.PlayerPawn.Value.Flags |= (uint)PlayerFlags.FL_ATCONTROLS;
					else cPlayer.PlayerPawn.Value.Flags &= ~(uint)PlayerFlags.FL_ATCONTROLS; //FL_ATCONTROLS (1<<6)
				}
			}
			foreach (var GTest in g_GameUI.ToList())
			{
				if (GTest.GameUI == cGameUI)
				{
					if (bActivate) GTest.cActivator = cActivator;
					else GTest.cActivator = null;
					Server.NextWorldUpdate(() =>
					{
						if (cActivator != null && cActivator.IsValid && cGameUI != null && cGameUI.IsValid)
							GTest.GameUI.AcceptInput("InValue", cActivator, GTest.GameUI, bActivate ? "PlayerOn" : "PlayerOff");
					});
				}
			}
		}
		void OnGameUIEventDeactivate(CCSPlayerController? cPlayer)
		{
			if (cPlayer != null && cPlayer.IsValid && cPlayer.Pawn != null && cPlayer.Pawn.IsValid)
			{
				foreach (var GTest in g_GameUI.ToList())
				{
					if (GTest.cActivator?.Index == cPlayer.Pawn.Index) GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
				}
			}
		}
		public static bool IsGameUI(CEntityInstance entity)
		{
			if (entity != null && entity.IsValid && string.Equals(entity.DesignerName, "logic_case") && !string.IsNullOrEmpty(entity.PrivateVScripts) && string.Equals(entity.PrivateVScripts.ToLower(), "game_ui")) return true;
			return false;
		}
		public static bool IsViewControl(CEntityInstance entity)
		{
			if (entity != null && entity.IsValid && string.Equals(entity.DesignerName, "logic_relay") && !string.IsNullOrEmpty(entity.PrivateVScripts) && string.Equals(entity.PrivateVScripts.ToLower(), "point_viewcontrol")) return true;
			return false;
		}
		public bool IsTarget(CEntityInstance entity)
		{
			if (entity != null && entity.IsValid)
			{
				foreach (var vc in g_ViewControl.ToList())
				{
					if(vc.Target == entity) return true;
				}
			}
			return false;
		}
		public static CCSPlayerController? EntityIsPlayer(CEntityInstance? entity)
		{
			if (entity != null && entity.IsValid && string.Equals(entity.DesignerName, "player"))
			{
				var pawn = new CCSPlayerPawn(entity.Handle);
				if (pawn.Controller.Value != null && pawn.Controller.Value.IsValid)
				{
					var player = new CCSPlayerController(pawn.Controller.Value.Handle);
					if (player != null && player.IsValid) return player;
				}
			}
			return null;
		}
		public static bool IsPlayerAlive(CCSPlayerController controller)
		{
			if (controller.Slot == 32766) return false;

			if (controller.LifeState == (byte)LifeState_t.LIFE_ALIVE || controller.PawnIsAlive) return true;
			else return false;
		}
		public static void PrintToConsole(string sMessage)
		{
			Console.ForegroundColor = (ConsoleColor)8;
			Console.Write("[");
			Console.ForegroundColor = (ConsoleColor)6;
			Console.Write("EntityFix");
			Console.ForegroundColor = (ConsoleColor)8;
			Console.Write("] ");
			Console.ForegroundColor = (ConsoleColor)3;
			Console.WriteLine(sMessage);
			Console.ResetColor();
		}
	}
}
