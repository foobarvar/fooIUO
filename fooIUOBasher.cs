using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using RazorEnhanced;

namespace fooIUO
{
    public class fooIUOBasher
    {
        /// <summary>
        /// Defines whether or not the script shall attempt to automatically bank gold via a bag of sending.
        /// </summary>
        public bool AutoBankGold { get; private set; } = true;        
        
        /// <summary>
        /// Sets the numeric limit for gold and the weight percentage limit to be reached before the AutoBanking
        /// function fires. The more loot you accumulate - besides gold -, the heavier the character gets. Since
        /// you will not be able to carry around 50000 gold pieces when your inventory already has 250 stones of 
        /// other loot, an additional weight limit is needed.
        /// Both properties will usually be slightly overshot because they represent thresholds.
        /// </summary>
        public int GoldLimit { get; private set; } = 50000;
        public int WeightPercentageLimit { get; private set; } = 90;

        /// <summary>
        /// Defines whether or not you are running Lootmaster - the only Lootscript that matters by the man
        /// himself, DORANA. The Automatic Banking does not always play nice with Lootmaster, so if this is
        /// set to true, the script will attempt to STOP Lootmaster before the banking process and START it
        /// again when the banking has finished.
        /// </summary>
        public bool UseLootmaster { get; set; } = true;


        /// <summary>
        /// Defines the maximum aggression distance in tiles. The script will identify viable mobiles as targets
        /// if they are within range of the number of tiles this property is set to. If this distance is longer
        /// than 10 tiles, Shield Bash may run out before the mobile reaches the player. Dependant on the playstyle,
        /// this could probably use some adjustment.
        /// </summary>
        public int MaxAggroRange { get; private set; } = 10;

        /// <summary>
        /// The serial (!) of the anti-paralyze crate. Leave at 0 if you have none, but you should really have one.
        /// Serial can be given as decimal or hexadecimal integer.
        /// </summary>
        public int AntiParalyzeCrate { get; private set; } = 0;

        

        /// <summary>
        /// Sets whether or not the script shall attempt to automatically cure the Blood Oath curse whenever it
        /// detects it. If the script finds enchanted apples in the player's inventory, it will use these. If there
        /// are no enchanted apples to be found, the script will attempt to cure via the Remove Curse spell. It will
        /// stop attacking during that time, but the UO autoattack will still work.
        /// Set this to false if you wish to skip this.
        /// </summary>
        public bool AutoCureBloodOath { get; set; } = true; 


        /// <summary>
        /// The time delay to elapse until the script starts the next cycle. On slower computers this can be raised
        /// but should probably not exceed 250 milliseconds. Raising this will also raise the frequency of target
        /// aquisition which can make the whole experience less "smooth".
        /// Should not be below 25 ms for the time being.
        /// </summary>
        public int ParseDelay { get; private set; } = 25;


        /// <summary>
        /// Sets the targeting mode. 1 is the default and targets only neutral and evil mobiles - grey and red
        /// stuff. If this is set to 0, blue mobiles are added to the potential targets. Useful for hunting
        /// Cu Sidhes or Unicorns. Even if this is set to 0, the standard filter will still apply.
        /// </summary>
        public int TargetingMode { get; private set; } = 1;

        /// <summary>
        /// Sets the filtering mode. If set to true, the _doNotTarget field defined below will exempt certain
        /// targets from being attacked by the script. If it is set to false instead, absolutely every mobile
        /// will be attacked.
        /// </summary>
        public bool FilterTargets { get; private set; } = true;
        
        /// <summary>
        /// Internal storage for the current target. Do not modify.
        /// </summary>
        public Mobile CurrentTarget { get; set; }

        /// <summary>
        /// Taken from ServUO code, used in cast delay calculations. Do not modify.
        /// </summary>
        public static readonly double HighFrequency = 1000.0 / Stopwatch.Frequency;
        public static double Ticks { get { return Stopwatch.GetTimestamp() * HighFrequency; } }

        /// <summary>
        /// The script will check the properties of each target against this list. If it finds any substring from this list
        /// in the target's properties, the target will be deemed as invalid and be exempt from attacking. Use lowercase
        /// when adding entries, since the properties from each Mobile will transformed to lowercase as well.
        /// </summary>
        private readonly List<string> _doNotTarget = new List<string>
        {
            "healer", "priest of mondain",
            "a dog", "a cat", "a horse", "a sheep", "a crane", "a sheep", "a cow", "a bull", "a chicken", "a pig", "a boar",
            "a dolphin", "a cu sidhe",
            "a pack horse", "a pack llama", "(summoned)", "bonded", "Loyalty"
        };


        /// <summary>
        /// UO hues for internal messaging. Can be customized to personal preferences.
        /// </summary>
        private int _red = 138;
        private int _yellow = 53;
        private int _green = 78;
        private int _blue = 188;


        /// <summary>
        /// The Entry point for Razor Enhanced. Everything is hooked up to here.
        /// </summary>
        public void Run()
        {
            CheckPlayer();
            Player.WeaponClearSA();

            while (true)
            {
                if (!IsPlayerMoving())
                {
                    BreakParalysis();
                    Misc.Pause(10);
                    CureBloodOath();
                    Misc.Pause(10);
                    BankGold();
                    Misc.Pause(10);

                    List<Mobile> targets = GenerateTargetList();

                    if (targets.Count > 0)
                    {
                        AttackNextTarget(targets.First());
                        Misc.Pause(ParseDelay);
                        UseSpecialAttack();
                        Misc.Pause(ParseDelay);
                    }
                }
            }
        }

        /// <summary>
        /// Displays the opening status checks.
        /// </summary>
        private void CheckPlayer()
        {
            Misc.SendMessage($"foo> Welcome too fooBasher!", _green);
            Misc.Pause(ParseDelay * 4);
            Misc.SendMessage($"foo> Checking character ...", _blue);
            Misc.SendMessage($"foo> Player STR: {Player.Str}", _blue);
            Misc.SendMessage($"foo> Player DEX: {Player.Dex}", _blue);
            Misc.SendMessage($"foo> Player INT: {Player.Int}", _blue);
            Misc.Pause(ParseDelay * 4);
            Misc.SendMessage($"foo> Player HP:  {Player.Hits}", _blue);
            Misc.SendMessage($"foo> Player MP:  {Player.Mana}", _blue);
            Misc.SendMessage($"foo> Player SP:  {Player.Stam}", _blue);
            Misc.Pause(ParseDelay * 4);
            Misc.SendMessage($"foo> Player LMC: {Player.LowerManaCost}", _blue);
            Misc.SendMessage($"foo> Player FC:  {Player.FasterCasting}", _blue);
            Misc.SendMessage($"foo> Player FCR: {Player.FasterCastRecovery}", _blue);
        }



        /// <summary>
        /// Imports the GetAsyncKeyState method from Windows' user32.dll.
        /// Used in the check of whether or not the player is moving.
        /// </summary>
        /// <param name="vKey">the hex id of the key in question, 0x02 for the right mouse button</param>
        /// <returns>the handle of GetAsyncKeyState</returns>
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);


        /// <summary>
        /// Checks the state of the right mouse button. If it is DOWN, the script assumes the player
        /// to be moving and thus pauses. The sleep timer represents the parsing interval, here the 
        /// CPU clock seems to be more trustworthy than whatever RE is doing with Misc.Pause().
        /// </summary>
        /// <returns>true if right mouse button is down, false if it is not</returns>
        private bool IsPlayerMoving()
        {
            Thread.Sleep(ParseDelay);   // dirty, but working - should not be lower than 25
            return (GetAsyncKeyState(0x02) & 0x8000) != 0;
        }


        /// <summary>
        /// Breaks paralysis by using the trapped box defined in the property AntiParalysisCrate.
        /// If no box is found, nothing happens.
        /// </summary>
        private void BreakParalysis()
        {
            if (Player.BuffsExist("Paralysis"))
            {
                Item crate = Player.Backpack.Contains.Where(x => x.Serial == AntiParalyzeCrate).FirstOrDefault();

                if (crate != null)
                {
                    Misc.SendMessage($"foo> Breaking paralysis ...", _yellow);
                    Items.UseItem(crate);
                    Misc.Pause(ParseDelay);
                }
            }
        }


        /// <summary>
        /// Attempts to cure the Blood Oath curse - the only curse or debuff that needs INSTANT attention.
        /// If no enchanted apples are found in the player's inventory, the script will attempt to cure 
        /// Blood Oath via the Chivalry spell Remove Curse. During this attacking will cease.
        /// It is strongly recommend to always carry a number of enchanted apples, because eating those 
        /// will cure any debuff, costs no mana and cannot be interrupted.
        /// </summary>
        private void CureBloodOath()
        {
            if (!AutoCureBloodOath)
            {
                return;
            }

            Item enchantedApples = Player.Backpack.Contains.Where(x => x.ItemID == 0x2FD8 && x.Color == 0x0488).FirstOrDefault();

            if (Player.BuffsExist("Blood Oath (curse)"))
            {
                if (enchantedApples != null)
                {
                    Items.UseItem(enchantedApples);
                    Misc.SendMessage($"foo> Enchanted apple eaten. Blood Oath cured!", _green);
                }
                else
                {
                    Misc.SendMessage($"foo> No enchanted apples found, attempting to cure Blood Oath by Remove Curse.", _blue);

                    while (Player.BuffsExist("Blood Oath (curse)"))
                    {
                        Spells.CastChivalry("Remove Curse");
                        Target.WaitForTarget(1000, true);
                        Target.Self();
                        Misc.Pause(ParseDelay);
                    }
                }
            }
        }


        /// <summary>
        /// Adds all mobiles within the defined maximum range to a list and filters. Filters applied
        /// work by notoriety and excluding keywords in the properties of the mobile (i. e. "healer").
        /// Finally sorts the targets by distance to the player (ascending).
        /// </summary>
        /// <returns>a filtered and sorted list of targets</returns>
        private List<Mobile> GenerateTargetList()
        {
            List<byte> notorieties = new List<byte>();

            switch (TargetingMode)
            {
                // targets ALL viable Mobiles, including "good" ones
                case 0:
                    notorieties.Add(1);
                    notorieties.Add(2);
                    notorieties.Add(3);
                    break;
                
                // targets only "neutral / grey" Mobiles
                case 1:
                    notorieties.Add(3);
                    break;
            }

            // targets "evil" Mobiles
            notorieties.AddRange(new List<byte> { 4, 5, 6 });

            Mobiles.Filter targetFilter = new Mobiles.Filter
            {
                RangeMin = 0,
                RangeMax = MaxAggroRange,
                IsGhost = 0,
                Friend = 0,
                CheckLineOfSight = true,
                CheckIgnoreObject = true,
                Notorieties = notorieties
            };

            List<Mobile> targets = Mobiles.ApplyFilter(targetFilter);

            if (FilterTargets)
            {
                targets.RemoveAll(IsInvalidTarget);
            }
            
            targets.Sort((x, y) => Player.DistanceTo(x).CompareTo(Player.DistanceTo(y)));
            return targets;
        }

        
        /// <summary>
        /// Checks if the passed mobile has any properties which exempt it from being a target.
        /// Mobiles are checked against the _doNotTarget field.
        /// </summary>
        /// <param name="mobile">the mobile to check</param>
        /// <returns>true for exemption, false for valid target</returns>
        private bool IsInvalidTarget(Mobile mobile)
        {
            string properties = String.Join(" ", mobile.Properties).ToLower();
            return _doNotTarget.Any(x => properties.Contains(x));
        }


        /// <summary>
        /// Returns the number of targets which are in close proximity to the player - these targets
        /// allow the use of Whirlwind if there are at least 2. Detection range is 4 tiles.
        /// Used in the selection of the special attacks.
        /// </summary>
        /// <returns>the number of targets within 4 tiles of the player</returns>
        private int CountTargetsInProximity()
        {
            return GenerateTargetList().Where(t => (Player.DistanceTo(t) < 4)).ToList().Count;
        }


        /// <summary>
        /// Returns the player's LMC stat factorized as a float, used for mana calculations for special attacks.
        /// </summary>
        /// <returns>the LMC factor</returns>
        private float GetLMCFactor()
        {
            return Player.LowerManaCost / 100;
        }


        /// <summary>
        /// Checks whether or not the passed target (from the target list) is still alive or has
        /// already died. Used in check for the special attacks, with bad luck a normal hit kills
        /// a target just after the last iteration of bashing fired - in this case the reference
        /// to the target list might still be there and the script would attack a Mobile that has
        /// since become an Item.
        /// </summary>
        /// <param name="mobile">an entry from the target list</param>
        /// <returns>true when Mobile still exists, false if not</returns>
        private bool IsAlive(Mobile mobile)
        {
            return Mobiles.FindBySerial(mobile.Serial) != null ? true : false;
        }


        /// <summary>
        /// Calculates the actual cast time for Shield Bash based on the characters FC stat.
        /// Base cast time of Shield Bash is 1 second. Maximum acceleration is reached
        /// with FC 3, minimum cast time will always be 250 milliseconds.
        /// </summary>
        /// <returns>adjusted cast delay in milliseconds</returns>
        private int GetShieldBashDelay()
        {
            return (1000 - (Math.Min(Player.FasterCasting, 3) * 250));
        }


        /// <summary>
        /// Calculates the required spell delay for Shield based on the player's FCR stat.
        /// Taken from ServUO directly, might be refactored in later versions.
        /// </summary>
        /// <returns>adjusted spell delay in milliseconds</returns>
        private int GetSpellDelay()
        {
            int crBase = 6;         // Base Cast Recovery (units)
            int crPerSecond = 4;    // Recovered per second

            int fcr = Player.FasterCastRecovery;
            int fcrDelay = -(1 * fcr);
            int spellDelay = Math.Max(crBase + fcrDelay, 0);

            return (int)TimeSpan.FromSeconds((double)spellDelay / crPerSecond).TotalMilliseconds;
        }


        /// <summary>
        /// Pulls the next target from the target list and attacks it
        /// </summary>
        private void AttackNextTarget(Mobile target)
        {
            CurrentTarget = target;

            if (CurrentTarget != null)
            {
                Player.Attack(CurrentTarget);
                Misc.Pause(25);
            }
        }


        /// <summary>
        /// Executes the best possible special attack based on number of proximity targets and mana.
        /// Can be refactored into a single if-else-block but the current state makes it easier to
        /// add more weapons for the user.
        /// Order of selection:
        /// 1. Shield Bash + Armor Ignore (Whirlwind) -> 40 + 30 = 70 mana (40 + 15 = 55) (* LMC factor)
        /// 2. Shield Bash solo -> 40 mana (* LMC factor) -> single target FOR ALL weapons
        /// 3. Armor Ignore / Whirlwind solo -> 30 / 15 mana (* LMC factor)
        /// 4. normal basic attack
        /// </summary>
        private void UseSpecialAttack()
        {
            int proximity = CountTargetsInProximity();
            Item weapon = Player.GetItemOnLayer("RightHand");

            if (weapon != null)
            {
                switch (weapon.ItemID)
                {
                    /* single target weapons with Armor Ignore as PRIMARY special ability */
                    case 0x13B0:    // war axe

                        if (proximity > 0 && Player.Mana >= Math.Ceiling(70 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                            Player.WeaponPrimarySA();
                            Misc.Pause(GetSpellDelay());
                        }
                        else if (proximity > 0 && Player.Mana >= Math.Ceiling(40 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                        }
                        else if (proximity > 0 && Player.Mana >= Math.Ceiling(30 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Player.WeaponPrimarySA();
                            Misc.Pause(GetSpellDelay());
                        }

                        break;

                    /* single target weapons with Armor Ignore as SECONDARY special ability */
                    case 0x13FF:    // katana (all)
                    case 0x0F5E:    // broadsword
                    case 0x2D22:    // leaf blade
                    case 0x0F61:    // longsword

                        if (proximity > 0 && Player.Mana >= Math.Ceiling(70 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                            Player.WeaponSecondarySA();
                            Misc.Pause(GetSpellDelay());
                        }
                        else if (proximity > 0 && Player.Mana >= Math.Ceiling(40 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                        }
                        else if (proximity > 0 && Player.Mana >= Math.Ceiling(30 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Player.WeaponSecondarySA();
                            Misc.Pause(GetSpellDelay());
                        }

                        break;

                    /* multi target weapons with Whirlwind as PRIMARY special ability  */
                    case 0x2D33:    // radiant scimitar

                        if (proximity >= 2 && Player.Mana >= Math.Ceiling(55 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                            Player.WeaponPrimarySA();
                            Misc.Pause(GetSpellDelay());
                        }
                        else if (proximity > 0 && Player.Mana >= Math.Ceiling(30 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                        }
                        else if (proximity >= 2 && Player.Mana >= Math.Ceiling(15 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Player.WeaponPrimarySA();
                            Misc.Pause(GetSpellDelay());
                        }

                        break;

                    /* multi target weapons with Whirlwind as SECONDARY special ability  */
                    case 0xA28B:    // bladed whip
                    case 0xA292:    // spiked whip
                    case 0xA289:    // barbed whip

                        if (proximity >= 2 && Player.Mana >= Math.Ceiling(55 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                            Player.WeaponSecondarySA();
                            Misc.Pause(GetSpellDelay());
                        }
                        else if (proximity > 0 && Player.Mana >= Math.Ceiling(30 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Spells.CastMastery("Shield Bash");
                            Misc.Pause(GetShieldBashDelay());
                        }
                        else if (proximity >= 2 && Player.Mana >= Math.Ceiling(15 * GetLMCFactor()) && IsAlive(CurrentTarget))
                        {
                            Player.WeaponSecondarySA();
                            Misc.Pause(GetSpellDelay());
                        }

                        break;
                }
            }
        }


        /// <summary>
        /// Finds the bag of sending in the player's backpack.
        /// </summary>
        /// <returns>the first bag of sending found or null if there is none</returns>
        private Item GetBagOfSending()
        {
            return Player.Backpack.Contains.Where(x => x.Name == "a bag of sending").FirstOrDefault();
        }


        /// <summary>
        /// Reads the remaining charges on the bag of sending and returns those as an integer.
        /// Parsing is done from the item properties.
        /// </summary>
        /// <param name="bag">the bag of sending to check (as Item object)</param>
        /// <returns>the number of remaining charges</returns>
        private int GetCharges(Item bag)
        {
            Property charges = bag.Properties.Last();
            string value = Regex.Match(charges.ToString(), @"\d+").Value;
            return Int32.Parse(value);
        }


        /// <summary>
        /// Automatically banks gold via the bag of sending if the property AutoBankGold is set to true.
        /// If Lootmaster is detected, it will be stopped and restarted after the banking procedure.
        /// </summary>
        private void BankGold()
        {
            if (!AutoBankGold)
            {
                return;
            }

            Item goldStack = Player.Backpack.Contains.Where(x => x.ItemID == 0x0EED && x.Color == 0x000).FirstOrDefault();
            Item bagOfSending = GetBagOfSending();

            if (goldStack != null && bagOfSending != null && GetCharges(bagOfSending) > 0)
            {
                if (goldStack.Amount >= GoldLimit || Player.Weight >= (Player.MaxWeight * ((float) WeightPercentageLimit / 100)))
                {
                    if (UseLootmaster && Misc.ScriptStatus("Lootmaster.cs"))
                    {
                        Misc.ScriptStop("Lootmaster.cs");
                        Misc.Pause(ParseDelay);
                    }

                    Misc.SendMessage($"foo> Attempting to bank {goldStack.Amount} gold.", _blue);

                    Journal journal = new Journal();
                    journal.Clear();

                    Items.UseItem(bagOfSending);
                    Target.WaitForTarget(1000, true);
                    Target.TargetExecute(goldStack);
                    Misc.Pause(ParseDelay * 12);

                    if (journal.Search("was deposited"))
                    {
                        Misc.SendMessage($"foo> {goldStack.Amount} successfully banked.", _green);
                        Misc.SendMessage($"foo> Your bag of sending has {GetCharges(bagOfSending)} charges left.", _blue);
                    }

                    if (UseLootmaster)
                    {
                        Misc.ScriptRun("Lootmaster.cs");
                    }
                }

            }
            else if (GetCharges(bagOfSending) == 0)
            {
                Misc.SendMessage($"foo> Your bag of sending has run out of charges.", _red);
            }
        }
    }
}