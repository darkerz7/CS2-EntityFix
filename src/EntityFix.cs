using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2_EntityFix
{
	public class CUtlSymbolLarge : NativeObject
	{
		public CUtlSymbolLarge(IntPtr pointer) : base(pointer) { }
		public string KeyValue => Utilities.ReadStringUtf8(Handle + 0);
	}
	public class CGameUI
	{
		public CEntityInstance? cActivator;
		public CLogicCase GameUI;
		public PlayerButtons LastButtonState;
		public CGameUI(CLogicCase gameUI){ cActivator = null; GameUI = gameUI; }
	}
	public class CIgnite
	{
		public CEntityInstance? cEntity;
		public CParticleSystem? cParticle;
		public double fEnd;
		public CounterStrikeSharp.API.Modules.Timers.Timer? timer;
		public CIgnite(CEntityInstance? cEnt, CParticleSystem? cPart, double fEnds, CounterStrikeSharp.API.Modules.Timers.Timer? tim)
		{
			cEntity = cEnt;
			cParticle = cPart;
			fEnd = fEnds;
			timer = tim;
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
		public static MemoryFunctionVoid<CEntityIdentity, CUtlSymbolLarge, CEntityInstance, CEntityInstance, CVariant, int> CEntityIdentity_AcceptInputFunc = new(GameData.GetSignature("CEntityIdentity_AcceptInput"));
		List<CGameUI> g_GameUI = new List<CGameUI>();
		List<CIgnite> g_Ignite = new List<CIgnite>();
		float g_VelocityIgnite = 0.2f;
		int g_DamageIgnite = 5;
		string g_IgnitePath = "particles/burning_fx/env_fire_small.vpcf";
		public override string ModuleName => "Entity Fix";
		public override string ModuleDescription => "Fixes game_player_equip, game_ui, IgniteLifeTime";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.0";
		public override void Load(bool hotReload)
		{
			RegisterListener<OnServerPrecacheResources>(OnPrecacheResources);
			CEntityIdentity_AcceptInputFunc.Hook(OnInput, HookMode.Pre);
			RegisterListener<OnEntitySpawned>(OnEntitySpawned_Listener);
			RegisterListener<OnEntityDeleted>(OnEntityDeleted_Listener);
			RegisterListener<OnTick>(OnOnTick_Listener);
			RegisterEventHandler<EventRoundStart>(OnEventRoundStart);
			RegisterEventHandler<EventRoundEnd>(OnEventRoundEnd);
			RegisterEventHandler<EventPlayerDeath>(OnEventPlayerDeathPost);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
		}
		public override void Unload(bool hotReload)
		{
			RemoveListener<OnServerPrecacheResources>(OnPrecacheResources);
			CEntityIdentity_AcceptInputFunc.Unhook(OnInput, HookMode.Pre);
			RemoveListener<OnEntitySpawned>(OnEntitySpawned_Listener);
			RemoveListener<OnEntityDeleted>(OnEntityDeleted_Listener);
			RemoveListener<OnTick>(OnOnTick_Listener);
			DeregisterEventHandler<EventRoundStart>(OnEventRoundStart);
			DeregisterEventHandler<EventRoundEnd>(OnEventRoundEnd);
			DeregisterEventHandler<EventPlayerDeath>(OnEventPlayerDeathPost);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
		}
		private void OnPrecacheResources(ResourceManifest manifest)
		{
			manifest.AddResource(g_IgnitePath);
		}
		private void OnEntitySpawned_Listener(CEntityInstance entity)
		{
			if (!IsGameUI(entity)) return;
			CGameUI gameui = new CGameUI(new CLogicCase(entity.Handle));
			g_GameUI.Add(gameui);
		}
		private void OnEntityDeleted_Listener(CEntityInstance entity)
		{
			if (!IsGameUI(entity)) return;
			CLogicCase gameui = new CLogicCase(entity.Handle);
			if ((gameui.Spawnflags & 32) == 0)
			{
				foreach (var GTest in g_GameUI.ToList())
				{
					if (GTest.GameUI == gameui)
					{
						Server.NextFrame(() =>
						{
							if (GTest.cActivator != null)
							{
								GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
							}
							Server.NextFrame(() =>
							{
								g_GameUI.Remove(GTest);
							});
						});
					}
				}
			}
		}
		private void OnOnTick_Listener()
		{
			foreach (var GTest in g_GameUI.ToList())
			{
				if (GTest.cActivator != null)
				{
					var player = new CCSPlayerController(new CCSPlayerPawn(GTest.cActivator.Handle).Controller.Value.Handle);
					if((GTest.GameUI.Spawnflags & 256) != 0 && (player.Buttons & PlayerButtons.Jump) != 0)
					{
						GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
						return;
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
					if ((buttonChanged & (PlayerButtons)34359738368) != 0)
					{
						GTest.GameUI.AcceptInput("InValue", GTest.cActivator, GTest.GameUI, (GTest.LastButtonState & (PlayerButtons)34359738368) != 0 ? "UnpressedLook" : "PressedLook");
					}

					GTest.LastButtonState = player.Buttons;
				}
			}
		}
		private HookResult OnEventRoundStart(EventRoundStart @event, GameEventInfo info)
		{
			foreach (var IgniteCheck in g_Ignite.ToList())
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
				if (IgniteCheck.cEntity.DesignerName.CompareTo("player") == 0)
				{
					CCSPlayerPawn ccspawn = new CCSPlayerPawn(IgniteCheck.cEntity.Handle);
					if (ccspawn != null && ccspawn.IsValid)
					{
						ccspawn.VelocityModifier = 1.0f;
					}
				}
			}
			Server.NextFrame(g_Ignite.Clear);
			return HookResult.Continue;
		}
		private HookResult OnEventRoundEnd(EventRoundEnd @event, GameEventInfo info)
		{
			g_GameUI.Clear();
			foreach (var IgniteCheck in g_Ignite.ToList())
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
				if (IgniteCheck.cEntity.DesignerName.CompareTo("player") == 0)
				{
					CCSPlayerPawn ccspawn = new CCSPlayerPawn(IgniteCheck.cEntity.Handle);
					if (ccspawn != null && ccspawn.IsValid)
					{
						ccspawn.VelocityModifier = 1.0f;
					}
				}
			}
			Server.NextFrame(g_Ignite.Clear);
			return HookResult.Continue;
		}
		[GameEventHandler(mode: HookMode.Post)]
		private HookResult OnEventPlayerDeathPost(EventPlayerDeath @event, GameEventInfo info)
		{
			if (@event.Userid != null && @event.Userid.Pawn != null)
			{
				foreach (var GTest in g_GameUI.ToList())
				{
					if (GTest.cActivator?.Index == @event.Userid.Pawn.Index)
					{
						GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
					}
				}
			}
			return HookResult.Continue;
		}
		[GameEventHandler]
		private HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			if (@event.Userid != null && @event.Userid.Pawn != null)
			{
				foreach (var GTest in g_GameUI.ToList())
				{
					if (GTest.cActivator?.Index == @event.Userid.Pawn.Index)
					{
						GTest.GameUI.AcceptInput("Deactivate", GTest.cActivator, GTest.GameUI);
					}
				}
			}
			return HookResult.Continue;
		}
		private HookResult OnInput(DynamicHook hook)
		{
			var cEntity = hook.GetParam<CEntityIdentity>(0);
			var cInput = hook.GetParam<CUtlSymbolLarge>(1);
			var cActivator = hook.GetParam<CEntityInstance>(2);
			var cCaller = hook.GetParam<CEntityInstance>(3);
			var cValue = new CUtlSymbolLarge(hook.GetParam<CVariant>(4).Handle);
			if (cInput.KeyValue.ToLower().Contains("ignitel"))
			{
				float fDuration;
				if (float.TryParse(cValue.KeyValue, out fDuration))
				{
					IgnitePawn(cActivator, fDuration);
				}
			} else if (cEntity.DesignerName.CompareTo("game_player_equip") == 0)
			{
				var ent = new CGamePlayerEquip(cEntity.EntityInstance.Handle);
				if (((EquipFlags)ent.Spawnflags).HasFlag(EquipFlags.SF_PLAYEREQUIP_STRIPFIRST))
				{
					if (cInput.KeyValue.ToLower().CompareTo("use") == 0 || cInput.KeyValue.ToLower().CompareTo("triggerforactivatedplayer") == 0)
					{
						if (cActivator.DesignerName.CompareTo("player") == 0)
						{
							var player = new CCSPlayerController(new CCSPlayerPawn(cActivator.Handle).Controller.Value.Handle);
							if (player != null && player.IsValid && IsPlayerAlive(player))
							{
								player.RemoveWeapons();
								if (cInput.KeyValue.ToLower().CompareTo("triggerforactivatedplayer") == 0) player.GiveNamedItem(cValue.KeyValue);
							}
						}
					}else if(cInput.KeyValue.ToLower().CompareTo("triggerforallplayers") == 0)
					{
						Utilities.GetPlayers().Where(p => p is { IsValid: true, IsHLTV: false }).ToList().ForEach(pl =>
						{
							if (IsPlayerAlive(pl)) pl.RemoveWeapons();
						});
					}
				}
			} else if(IsGameUI(new CEntityInstance(cEntity.EntityInstance.Handle)))
			{
				if (cInput.KeyValue.ToLower().CompareTo("activate") == 0) OnActivateGameUI(cActivator, new CLogicCase(cEntity.EntityInstance.Handle));
				else if (cInput.KeyValue.ToLower().CompareTo("deactivate") == 0) OnDeactivateGameUI(cActivator, new CLogicCase(cEntity.EntityInstance.Handle));
				//Console.WriteLine($"{cEntity.DesignerName}/{cInput.KeyValue}:{cActivator.Index}:{cCaller.Index}***{cValue.KeyValue}***");
			}
			//Console.WriteLine($"{cEntity.DesignerName}/{cInput.KeyValue}:{cActivator.Index}:{cCaller.Index}***{cValue.KeyValue}***");
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
			
			CEntityInstance cActivatorBuf = new CEntityInstance(cActivator.Handle);
			CBaseEntity pawn = new CBaseEntity(cActivatorBuf.Handle);
			CParticleSystem? particle = null;
			if (pawn != null && pawn.IsValid)
			{
				particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
				if (particle != null && particle.IsValid)
				{
					particle.EffectName = g_IgnitePath;
					particle.TintCP = 1;
					particle.Tint = System.Drawing.Color.FromArgb(255, 255, 0, 0);
					particle.StartActive = true;

					particle.Teleport(pawn.AbsOrigin, pawn.AbsRotation, pawn.AbsVelocity);
					particle.DispatchSpawn();
				}
			}
			CIgnite cNewIgnite = new CIgnite(cActivatorBuf, particle, Server.EngineTime + fDuration, new CounterStrikeSharp.API.Modules.Timers.Timer(0.2f, () =>
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
									CBaseEntity pawn = new CBaseEntity(IgniteCheck.cEntity.Handle);
									IgniteCheck.cParticle.Teleport(pawn.AbsOrigin, pawn.AbsRotation, pawn.AbsVelocity);
								}
								if (cActivatorBuf.DesignerName.CompareTo("player") == 0)
								{
									CCSPlayerPawn ccspawn = new CCSPlayerPawn(cActivatorBuf.Handle);
									if (ccspawn != null && ccspawn.IsValid)
									{
										ccspawn.VelocityModifier = g_VelocityIgnite;
										ccspawn.Health -= g_DamageIgnite;
										Utilities.SetStateChanged(ccspawn, "CBaseEntity", "m_iHealth");
										if (ccspawn.Health <= 0) ccspawn.CommitSuicide(true, true);
									}
								}
								//Console.WriteLine($"Ignite: {cActivatorBuf.Index}. Left:{IgniteCheck.fEnd - Server.EngineTime}");
							} else
							{
								if (IgniteCheck.timer != null)
								{
									IgniteCheck.timer.Kill();
									IgniteCheck.timer = null;
								}
								if (IgniteCheck.cParticle != null)
								{
									IgniteCheck.cParticle.AcceptInput("Stop");
									IgniteCheck.cParticle.Remove();
								}
								if (IgniteCheck.cEntity.DesignerName.CompareTo("player") == 0)
								{
									CCSPlayerPawn ccspawn = new CCSPlayerPawn(IgniteCheck.cEntity.Handle);
									if (ccspawn != null && ccspawn.IsValid)
									{
										ccspawn.VelocityModifier = 1.0f;
									}
								}
								Server.NextFrame(() => g_Ignite.Remove(IgniteCheck));
							}
						}
					}
				}
				catch (Exception) { }
			}, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT));
			g_Ignite.Add(cNewIgnite);
		}
		void OnActivateGameUI(CEntityInstance cActivator, CLogicCase cGameUI)
		{
			if (cActivator == null || !cActivator.IsValid || cGameUI == null || !cGameUI.IsValid) return;
			if ((cGameUI.Spawnflags & 32) != 0)
			{
				if (cActivator.DesignerName.CompareTo("player") == 0)
				{
					CCSPlayerController cPlayer = new CCSPlayerController(new CCSPlayerPawn(cActivator.Handle).Controller.Value.Handle);
					cPlayer.Flags = cPlayer.Flags | (1 << 6); //FL_ATCONTROLS (1<<6)
				}
			}
			else
			{
				foreach (var GTest in g_GameUI.ToList())
				{
					if (GTest.GameUI == cGameUI)
					{
						GTest.cActivator = cActivator;
						Server.NextFrame(() =>
						{
							if (cActivator != null || cActivator.IsValid && cGameUI != null && cGameUI.IsValid)
								GTest.GameUI.AcceptInput("InValue", cActivator, GTest.GameUI, "PlayerOn");
						});
					}
				}
			}
		}
		void OnDeactivateGameUI(CEntityInstance cActivator, CLogicCase cGameUI)
		{
			if (cActivator == null || !cActivator.IsValid || cGameUI == null || !cGameUI.IsValid) return;
			if ((cGameUI.Spawnflags & 32) != 0)
			{
				if (cActivator.DesignerName.CompareTo("player") == 0)
				{
					CCSPlayerController cPlayer = new CCSPlayerController(new CCSPlayerPawn(cActivator.Handle).Controller.Value.Handle);
					cPlayer.Flags = cPlayer.Flags & (1 << 6); //FL_ATCONTROLS (1<<6)
				}
			}
			else
			{
				foreach (var GTest in g_GameUI.ToList())
				{
					if (GTest.GameUI == cGameUI)
					{
						GTest.cActivator = cActivator;
						Server.NextFrame(() =>
						{
							if (cActivator != null || cActivator.IsValid && cGameUI != null && cGameUI.IsValid)
							{
								GTest.GameUI.AcceptInput("InValue", cActivator, GTest.GameUI, "PlayerOff");
								GTest.cActivator = null;
							}
						});
					}
				}
			}
		}

		public static bool IsGameUI(CEntityInstance entity)
		{
			if (entity != null && entity.IsValid && entity.DesignerName.CompareTo("logic_case") == 0 && !string.IsNullOrEmpty(entity.PrivateVScripts) && entity.PrivateVScripts.ToLower().CompareTo("game_ui") == 0) return true;
			return false;
		}
		public static bool IsPlayerAlive(CCSPlayerController controller)
		{
			if (controller.Slot == 32766) return false;

			if (controller.LifeState == (byte)LifeState_t.LIFE_ALIVE || controller.PawnIsAlive) return true;
			else return false;
		}
	}
}
