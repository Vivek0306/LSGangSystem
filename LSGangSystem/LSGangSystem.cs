using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Windows.Forms;
using System.Collections.Generic;

public class LSGangSystem : Script
{
    private Ped gang_member = null;
    private List<Ped> gang_members = new List<Ped>();
    private float recruitRange = 3.0f;
    private int max_members = 4;

    private int notifyTimer = 0;
    private Dictionary<Ped, Blip> gang_blips = new Dictionary<Ped, Blip>();

    public LSGangSystem()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        Interval = 10; // 10 ms
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;

        // Show gang member count every 2 seconds (2000 ms = 200 ticks at 10 ms)
        notifyTimer++;
        if (notifyTimer >= 200)
        {
            GTA.UI.Notify($"~g~Gang Members: {gang_members.Count}/{max_members}");
            notifyTimer = 0;
        }

        if (gang_member == null || !gang_member.Exists() || gang_member.IsDead)
        {
            gang_member = GetNearbyPed();
        }

        if (gang_member != null)
        {
            GTA.UI.ShowSubtitle("Press ~y~Y~w~ to recruit, ~r~N~w~ to cancel");

            if (gang_member.Position.DistanceTo(Game.Player.Character.Position) > recruitRange)
                gang_member = null;
        }

        for (int i = gang_members.Count - 1; i >= 0; i--)
        {
            Ped member = gang_members[i];
            if (member.IsDead)
            {
                if (gang_blips.ContainsKey(member))
                {
                    gang_blips[member].Remove();
                    gang_blips.Remove(member);
                }
                gang_members.RemoveAt(i);
            }
        }

        if (Game.Player.Character.IsDead)
        {
            for (int i = gang_members.Count - 1; i >= 0; i--)
            {
                gang_members[i].Kill();
                gang_members.RemoveAt(i);
            }
        }

        Ped[] nearbyPeds = World.GetNearbyPeds(player, 30f);

        foreach (Ped ped in nearbyPeds)
        {
            if (!ped.IsDead && ped != player && !gang_members.Contains(ped))
            {
                if (Function.Call<bool>(Hash.IS_PED_IN_COMBAT, ped.Handle, player.Handle))
                {
                    foreach (Ped gang in gang_members)
                    {
                        if (gang.Exists() && !gang.IsDead && gang != ped)
                        {
                            Function.Call(Hash.TASK_COMBAT_PED, gang.Handle, ped.Handle, 0, 16);
                            Function.Call(Hash.SET_PED_KEEP_TASK, gang.Handle, true);
                        }
                    }

                    break;
                }
            }
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        Ped player = Game.Player.Character;
        if (gang_member != null && gang_member.Exists())
        {
            if (e.KeyCode == Keys.Y)
            {
                RecruitPed(gang_member);
                gang_member = null;
            }
            else if (e.KeyCode == Keys.N)
            {
                GTA.UI.Notify("~r~Recruitment Cancelled!");
                gang_member = null;
            }
        }

        if (e.KeyCode == Keys.L)
        {
            if (gang_members.Count > 0)
            {
                foreach (Ped ped in gang_members)
                {
                    if (ped.Exists())
                    {
                        if (gang_blips.ContainsKey(ped))
                        {
                            gang_blips[ped].Remove();
                        }
                        Function.Call(Hash.REMOVE_PED_FROM_GROUP, ped.Handle);
                        ped.Task.ClearAllImmediately();
                        ped.IsPersistent = false;
                        ped.MarkAsNoLongerNeeded();
                    }
                }
                gang_blips.Clear();
                gang_members.Clear();
                GTA.UI.Notify("~r~All Gang Members Dismissed!");
            }
        }

        if (e.KeyCode == Keys.B)
        {
            Game.Player.WantedLevel = 0;
            GTA.UI.Notify("~b~Wanted level cleared!");
        }

        if (e.KeyCode == Keys.N)
        {
            SpawnEnemyPed();
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e) { }

    private void RecruitPed(Ped ped)
    {
        if (gang_members.Count >= max_members)
        {
            GTA.UI.Notify("~r~Gang is full!");
            return;
        }

        ped.IsPersistent = true;
        ped.BlockPermanentEvents = true;

        ped.Weapons.Give(GTA.Native.WeaponHash.Pistol, 100, true, true);

        gang_members.Add(ped);

        int playerGroup = Function.Call<int>(Hash.GET_PED_GROUP_INDEX, Game.Player.Character.Handle);

        Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, ped.Handle, playerGroup);
        // Function.Call(Hash.SET_PED_NEVER_LEAVES_GROUP, ped.Handle, true);

        ped.Task.ClearAllImmediately();
        Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_IN_AREA, ped, 50000, 0);
        Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);


        Blip blip = ped.AddBlip();
        blip.Color = BlipColor.Blue;
        blip.Scale = 0.7f;
        blip.Name = "Gang Member";
        gang_blips[ped] = blip;

        GTA.UI.Notify("~b~Gang member recruited!");
    }

    //Testing Purposes
    private void SpawnEnemyPed()
    {
        Ped player = Game.Player.Character;
        Ped enemyPed = World.CreatePed("a_m_m_mlcrisis_01", player.Position + (player.ForwardVector * 5));
    
        if (enemyPed != null && enemyPed.Exists())
        {
            enemyPed.Task.ClearAllImmediately();
            enemyPed.Task.FightAgainst(player);
            Function.Call(Hash.SET_PED_KEEP_TASK, enemyPed, true);
            UI.Notify("~b~Enemy Spawned!");
        }
        else
        {
            UI.Notify("~r~Failed to create Enemy!");
        }
    }
    private Ped GetNearbyPed()
    {
        Ped player = Game.Player.Character;
        Ped[] nearbyPeds = World.GetNearbyPeds(player, 10f);

        foreach (Ped ped in nearbyPeds)
        {
            if (ped != player && !ped.IsInCombat && !gang_members.Contains(ped) && ped.IsAlive)
            {
                if (ped.Position.DistanceTo(player.Position) < recruitRange)
                {
                    return ped;
                }
            }
        }

        return null;
    }
}