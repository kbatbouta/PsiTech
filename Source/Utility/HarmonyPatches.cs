/*
 *  Copyright 2019, 2020, K
 * 
 *  This file is part of PsiTech.
 *
 *  PsiTech is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Foobar is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with PsiTech. If not, see <https://www.gnu.org/licenses/>.
 *
 */

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using PsiTech.AI;
using PsiTech.AutocastManagement;
using PsiTech.Misc;
using PsiTech.Psionics;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PsiTech.Utility {
    
    [StaticConstructorOnStartup]
    public class HarmonyPatches {

        static HarmonyPatches() {
            var harmony = new Harmony("K.PsiTech");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public class PawnGizmosPatch {

        static void Postfix(ref IEnumerable<Gizmo> __result, ref Pawn __instance) {

            if (__instance?.PsiTracker()?.GetGizmos() == null) return;

            __result = __result.Concat(__instance.PsiTracker().GetGizmos());

        }
        
    }
    
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public class StatValuePatch {
        
        public static float Postfix(float __result, StatRequest req, StatDef ___stat) {
            if(!req.HasThing || !PsiTechCachingUtility.CachedAffectedStats.Contains(___stat)) return __result;
            
            if (req.Thing is Pawn pawn && pawn.PsiTracker().Activated) {
                __result += pawn.PsiTracker().GetTotalOffsetOfStat(___stat);
                __result *= pawn.PsiTracker().GetTotalFactorOfStat(___stat);
            }else if (req.Thing.PsiEquipmentTracker().IsPsychic) {
                var equip = req.Thing.TryGetComp<CompEquippable>();
                if (equip == null || !(equip.PrimaryVerb.caster is Pawn caster)) return __result;

                __result *= req.Thing.PsiEquipmentTracker().GetTotalFactorOfStat(___stat, caster);
            }

            return __result;
        }
        
    }

    [HarmonyPatch(typeof(StatWorker), "GetExplanationUnfinalized")]
    public class StatExplanationPatch {

        public static string Postfix(string __result, StatRequest req, StatDef ___stat) {

            if (!req.HasThing || !PsiTechCachingUtility.CachedAffectedStats.Contains(___stat)) return __result;

            if (req.Thing is Pawn pawn) {
                var sb = new StringBuilder();
                var abilities = pawn.PsiTracker().Abilities;

                if (abilities.Count > 0) {
                    sb.AppendLine("PsiTech.PsionicAbilitiesReport".Translate() + ":");
                }

                var didHaveEffect = false;
                foreach (var ability in abilities) {
                    var offset = ability.GetOffsetOfStat(___stat);
                    var factor = ability.GetFactorOfStat(___stat);

                    if (offset != 0) {
                        didHaveEffect = true;
                        sb.AppendLine("    " + ability.Def.label + ": " +
                                      offset.ToStringByStyle(___stat.ToStringStyleUnfinalized,
                                          ToStringNumberSense.Offset));
                    }

                    if (factor != 1) {
                        didHaveEffect = true;
                        sb.AppendLine("    " + ability.Def.label + ": " +
                                      factor.ToStringByStyle(___stat.ToStringStyleUnfinalized,
                                          ToStringNumberSense.Factor));
                    }
                }

                if (didHaveEffect) {
                    __result += "\n\n" + sb;
                }
            }else if (req.Thing.PsiEquipmentTracker()?.IsPsychic ?? false) {
                var equip = req.Thing.TryGetComp<CompEquippable>();
                if (equip == null || !(equip.PrimaryVerb.caster is Pawn caster)) return __result;

                var factor = req.Thing.PsiEquipmentTracker().GetTotalFactorOfStat(___stat, caster);
                if (factor != 1) {
                    __result += "\n\n" + "PsiTech.PsychicWeaponReport".Translate() +
                                factor.ToStringByStyle(___stat.toStringStyle, ToStringNumberSense.Factor);
                }
            }

            return __result;
        }
        
    }

    [HarmonyPatch(typeof(UIRoot_Play), "UIRootUpdate")]
    public class UIRootPatch {

        public static void Postfix() {
            PsiTechRenderUtility.PsiTechRenderUtilityUpdate();
        }
        
    }

    [HarmonyPatch(typeof(TooltipUtility), "ShotCalculationTipString")]
    public class TooltipUtilityPatch {
        
        public static string Postfix(string __result, Thing target) {
            
            if (Find.Selector.SingleSelectedThing == null || !(Find.Selector.SingleSelectedThing is Pawn pawn) ||
                !(target is Pawn targetPawn) || pawn.PsiTracker().CurrentAbility == null) return __result;

            var ability = pawn.PsiTracker().CurrentAbility;
            var sb = new StringBuilder();

            if (ability.Def.TargetValidator.IsValidTarget(pawn, targetPawn)) {
                sb.AppendLine("PsiTech.AbilityByPawn".Translate((NamedArgument) ability.Def.label,
                    (NamedArgument) pawn.LabelShortCap, ability.SuccessChanceOnTarget(targetPawn).ToStringPercent()));

                sb.AppendLine(
                    "   " + "PsiTech.BaseSuccessChance".Translate(ability.Def.BaseSuccessChance.ToStringPercent()));

                sb.AppendLine(
                    "   " + "PsiTech.AbilityModifier".Translate(pawn.PsiTracker().GetTotalModifierActive().ToStringPercent()));

                sb.AppendLine("   " + "PsiTech.TargetSensitivity"
                                  .Translate(targetPawn.PsiTracker().GetTotalModifierSensitivity().ToStringPercent()));
            }
            else {
                sb.AppendLine("PsiTech.AbilityNotValidOnTarget".Translate((NamedArgument) targetPawn.LabelShortCap,
                    (NamedArgument) ability.Def.label));
            }

            if (__result.NullOrEmpty()) { // If a pawn doesn't have a weapon, the result is only a blank line
                __result += sb;
            }
            else {
                __result += "\n" + sb;
            }
            

            return __result;
        }
        
    }

    [HarmonyPatch(typeof(PawnCapacityUtility), "CalculateCapacityLevel")]
    public class PawnCapacityPatch {

        public static float Postfix(float __result, HediffSet diffSet, PawnCapacityDef capacity,
            ref List<PawnCapacityUtility.CapacityImpactor> impactors) {

            if (capacity.zeroIfCannotBeAwake && !diffSet.pawn.health.capacities.CanBeAwake) return __result;

            var max = 999f;
            var originalMult = 1f;
            foreach (var hediff in diffSet.hediffs) {
                var capMod = hediff.CapMods?.Find(mod => mod.capacity == capacity);
                if (capMod == null) continue;

                originalMult *= capMod.postFactor;
                if (capMod.setMax < max) max = capMod.setMax;
            }

            var offset = diffSet.pawn.PsiTracker().GetTotalOffsetOfCapacity(capacity);
            var mult = diffSet.pawn.PsiTracker().GetTotalFactorOfCapacity(capacity);
            if (impactors != null) {
                var abilities = diffSet.pawn.PsiTracker().GetAllAbilitiesImpactingCapacity(capacity);
                impactors.AddRange(abilities.Select(ab => new CapacityImpactorPsychic{ability = ab}));
            }

            var newResult = __result * mult + offset * originalMult * mult;

            __result = Mathf.Min(newResult, max);

            return GenMath.RoundedHundredth(Mathf.Max(__result, capacity.minValue));
            
        }
    }

    // For use with the capacities patch
    public class CapacityImpactorPsychic : PawnCapacityUtility.CapacityImpactor {

        public PsiTechAbility ability;

        public override string Readable(Pawn pawn) {
            return $"{ability.Def.label}";
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "GetPawnCapacityTip")]
    public class HealthTooltipPatch {
        
        public static string Postfix(string __result, Pawn pawn, PawnCapacityDef capacity)
        {
            // So this might look a bit like dark magic, but what's happening is that the impactors list is populated
            // in CalculateCapacityLevel. Why isn't it an out parameter? Because it's optional.
            var impactors = new List<PawnCapacityUtility.CapacityImpactor>();
            PawnCapacityUtility.CalculateCapacityLevel(pawn.health.hediffSet, capacity, impactors);
            var sb = new StringBuilder();
            foreach (var impactor in impactors.FindAll(impact => impact is CapacityImpactorPsychic)) {
                sb.AppendLine($"  {impactor.Readable(pawn)}");
            }

            __result += sb;
            return __result;
        }
        
    }

    [HarmonyPatch(typeof(SkillRecord), "Level", MethodType.Getter)]
    public class SkillLevelPatch {

        public static int Postfix(int __result, Pawn ___pawn, SkillDef ___def) {

            if (!___pawn.IsColonist) return __result;
            
            var transferPairs = PsiTechSkillTransferUtility.GetActiveTransferPairsForReceiver(___pawn);
            foreach (var pair in transferPairs) {
                var initiatorSkillLevel = pair.Initiator.skills.GetSkill(___def).levelInt;
                if (initiatorSkillLevel > __result) {
                    __result += (int)((initiatorSkillLevel - __result) * pair.SkillTransferAmount);
                }
            }

            return __result;

        }
        
    }
    
    // Dodge damage from time to time, for Battlefield Precognition
    // Increase damage dealt by damage multiplier
    // Prefix because damage gets applied in the preapply method itself, so we need to dodge/increase before that
    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
    static class PawnPreApplyDamagePatch {

        static bool Prefix(ref DamageInfo dinfo, out bool absorbed, Pawn __instance) {
            var sensitivity = 0f; // Needed to prevent nullref for instigator-less damage (explosions, cave-ins, ect.)
            
            // When pawns are on fire, they're the instigator, which is a problem...
            if (dinfo.Def != DamageDefOf.Flame && dinfo.Instigator != __instance && dinfo.Instigator != null) {
                var mod = 1 + (dinfo.Instigator.GetStatValue(PsiTechDefOf.PTDamageMultiplier) - 1) *
                               __instance.GetStatValue(StatDefOf.PsychicSensitivity);
                dinfo.SetAmount(dinfo.Amount * mod);

                sensitivity = Mathf.Clamp(dinfo.Instigator.GetStatValue(StatDefOf.PsychicSensitivity), 0.5f, 2f);
            }

            var chance = Mathf.Clamp(__instance.GetStatValue(PsiTechDefOf.PTDamageDodgeChance) * sensitivity, 0,
                0.6f);
            if (!Rand.Chance(chance)) {
                absorbed = false;
                return true;
            }

            MoteMaker.ThrowText(__instance.Position.ToVector3(), __instance.Map, "TextMote_Dodge".Translate(), 1.9f);
            absorbed = true;
            return false;
        }
        
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "CheckForStateChange")]
    static class HealthTrackerStatePatch {

        static void Prefix(DamageInfo? dinfo, Pawn_HealthTracker __instance, Pawn ___pawn) {

            if (__instance.Dead || !___pawn.PsiTracker().Activated) return;

            var instigator = dinfo?.Instigator as Pawn;
            
            if (ShouldBeDead(__instance, ___pawn)) {
                ___pawn.PsiTracker().TriggerAlmostDead(instigator);
                return;
            }
            
            if (ShouldBeDowned(__instance)) {
                ___pawn.PsiTracker().TriggerAlmostDead(instigator);
            }
        }
        
        private static bool ShouldBeDowned(Pawn_HealthTracker tracker)
        {
            if (!tracker.InPainShock && tracker.capacities.CanBeAwake)
                return !tracker.capacities.CapableOf(PawnCapacityDefOf.Moving);
            return true;
        }

        private static bool ShouldBeDead(Pawn_HealthTracker tracker, Pawn pawn)
        {
            if (tracker.Dead)
                return true;
            foreach (var hediff in tracker.hediffSet.hediffs) {
                if (hediff.CauseDeathNow())
                    return true;
            }

            return tracker.ShouldBeDeadFromRequiredCapacity() != null ||
                   PawnCapacityUtility.CalculatePartEfficiency(tracker.hediffSet,
                       pawn.RaceProps.body.corePart) <= 1E-04 ||
                   tracker.ShouldBeDeadFromLethalDamageThreshold();
        }

    }
    
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PostApplyDamage")]
    static class HealthTrackerPostDamagePatch {

        static void Prefix(DamageInfo dinfo, Pawn ___pawn) {
            
            if (___pawn.Dead || !___pawn.PsiTracker().Activated || dinfo.Instigator == null ||
                dinfo.Instigator == ___pawn || !(dinfo.Instigator is Pawn instigator)) return;
            
            ___pawn.PsiTracker().TriggerAttacked(instigator);

        }
        
    }

    [HarmonyPatch(typeof(Toils_Recipe), "ConsumeIngredients")]
    static class ConsumeIngredientsPatch {

        // I might be crazy
        static void Prefix(ref List<Thing> ingredients, RecipeDef recipe) {
            
            if (recipe != PsiTechDefOf.PTUpgradeWeaponPsychic) return;
            
            if (!(ingredients.FirstOrDefault(t => t.def.IsWeapon) is ThingWithComps weapon)) {
                Log.Error("PsiTech tried to process a weapon upgrade without a weapon (this should never happen).");
                return;
            }

            ingredients.Remove(weapon);

            if (weapon.PsiEquipmentTracker() == null) {
                Log.Warning("PsiTech tried to upgrade a weapon that wasn't a ThingWithComps. This should never happen.");
                return;
            }
            
            if (weapon.PsiEquipmentTracker().IsPsychic) {
                Log.Warning("PsiTech tried to upgrade a weapon that was already psychic. This should never happen.");
                return;
            }
            weapon.PsiEquipmentTracker().IsPsychic = true;

        }
        
    }

    [HarmonyPatch(typeof(GenLabel), "ThingLabel", typeof(Thing), typeof(int), typeof(bool))]
    static class GenLabelPatch {

        static string Postfix(string __result, Thing t) {

            if (!t.PsiEquipmentTracker()?.IsPsychic ?? true) return __result;

            return __result + " (P)";

        }
        
    }

    [HarmonyPatch(typeof(StatsReportUtility), "DescriptionEntry", typeof(Thing))]
    static class ThingDescPatch {

        static void Postfix(ref StatDrawEntry __result, Thing thing) {
            
            if (!thing.PsiEquipmentTracker()?.IsPsychic ?? true) return;

            __result = new StatDrawEntry(StatCategoryDefOf.BasicsImportant, "Description".Translate(), "",
                thing.DescriptionFlavor + "PsiTech.PsychicWeaponDesc".Translate(), 99999, null,
                Dialog_InfoCard.DefsToHyperlinks(thing.def.descriptionHyperlinks));

        }
        
    }

    [HarmonyPatch(typeof(ThingWithComps), "GetGizmos")]
    public class ThingGizmosPatch {

        static void Postfix(ref IEnumerable<Gizmo> __result, ref Thing __instance) {

            if (__instance?.PsiEquipmentTracker()?.GetGizmos() == null) return;

            __result = __result.Concat(__instance.PsiEquipmentTracker().GetGizmos());

        }

    }

    // Armor penetration is weird - it doesn't function anything like other stats
    // The display value that's shown in the info panel is totally disconnected from the actual calculation, which
    // occurs in the VerbProperties, so we have to change the armor penetration when it's calculated for the tool...
    // Incidentally, this means that CyberNet has been broken since launch, but I don't really want to fix it
    // As long as no one reports it, right?
    [HarmonyPatch(typeof(VerbProperties), "AdjustedArmorPenetration", typeof(Tool), typeof(Pawn), typeof(Thing),
        typeof(HediffComp_VerbGiver))]
    public class ArmorPenetationPatch {

        public static float Postfix(float __result, Pawn attacker, Thing equipment) {

            var offset = 0f;
            var factor = 1f;
            var meleePen = StatDef.Named("MeleeArmorPenetration");
            if (attacker?.PsiTracker().Activated ?? false) {
                offset += attacker.PsiTracker().GetTotalOffsetOfStat(meleePen);
                factor *= attacker.PsiTracker().GetTotalFactorOfStat(meleePen);
            }

            // Glad I didn't try to fix this in CyberNet - this is absolute hacks
            if (attacker?.health.hediffSet.HasHediff(PsiTechDefOf.PTMeleeMastery) ?? false) {
                factor *= PsiTechDefOf.PTMeleeMastery.stages.First().statOffsets.First(stat => stat.stat == meleePen).value;
            }

            __result += offset;
            __result *= factor;

            return __result;
        }
        
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), "StartOrResumeBillJob")]
    public class DoBillPatch {

        public static Job Postfix(Job __result, Pawn pawn) {

            if (pawn == null || __result?.bill?.recipe != PsiTechDefOf.PTWorkbenchCreateAthenium) return __result;

            // We're trying to create athenium and need to make sure that the pawn is able to do so
            if (pawn.PsiTracker().HasAbility(PsiTechDefOf.PTPsiForging)) return __result;
            
            JobFailReason.Is("PsiTech.NeedPsiForging".Translate(), __result.bill.Label);
            return null;

        }
        
    }

    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public class AutoAttackPatch {

        public static bool Prefix(Pawn ___pawn) {

            if (!___pawn.PsiTracker().Activated || !___pawn.PsiTracker().AutocastEnabled) return true;
            
            var autocast = ___pawn.PsiTracker().GetAutocastJob();

            if (autocast == null) return true;
            
            ___pawn.jobs.StartJob(autocast, JobCondition.InterruptForced);
            return false;

        }
        
    }

    [HarmonyPatch(typeof(Pawn_StanceTracker), "CancelBusyStanceSoft")]
    public class CancelSoftPatch {

        public static void Postfix(Pawn_StanceTracker __instance) {
            if (!(__instance.curStance is Stance_PsiWarmup)) return;
            
            __instance.SetStance(new Stance_Mobile());
        }
        
    }

    // Psychic raid spawning - probably unfortunately slow but there's not too much I can do about that
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", typeof(PawnGenerationRequest))]
    public class PsiPawnGenerationPatch {

        public static void Postfix(ref Pawn __result, PawnGenerationRequest request) {

            if (request.Faction?.def != PsiTechDefOf.PTPsionic || !(request.KindDef is PsiTechPawnKindDef psiKind) ||
                psiKind.PsiAbilitiesMoney == FloatRange.Zero) return;
            
            // Remove psychically dull/deaf - can't enforce this in def since it's a spectrum trait...
            __result.story.traits.allTraits.RemoveAll(trait =>
                trait.def == TraitDefOf.PsychicSensitivity && trait.Degree < 0);
            
            // We've got psi ability money to burn
            var money = psiKind.PsiAbilitiesMoney.RandomInRange;

            // Prep pawn and get ability pool copy for manipulation
            __result.PsiTracker().Activated = true;
            __result.PsiTracker().Abilities.Clear();
            __result.PsiTracker().FocusLevel = psiKind.FocusRange.RandomInRange;
            __result.PsiTracker().EnergyLevel = psiKind.TotalLevelRange.RandomInRange - __result.PsiTracker().FocusLevel;
            
            var copiedPool = psiKind.AbilityPool.ListFullCopy();
            foreach (var ability in copiedPool.ListFullCopy()) {
                if (ability.AbilityCostForRaid <= money && __result.PsiTracker().HasAvailableSlot(ability.Tier)) continue;
                
                copiedPool.Remove(ability);
            }
            
            // Time to roll some abilities
            while (copiedPool.Any()) {

                PsiTechAbilityDef abilityToAdd;
                // Always give synchronicity to fire support types
                if (__result.GetStatValue(PsiTechDefOf.PTPsiWeaponSynchronicity) != 0 &&
                    copiedPool.Contains(PsiTechDefOf.PTPerfectedSynchronicity)) {
                    abilityToAdd = PsiTechDefOf.PTPerfectedSynchronicity;
                }
                else {
                    abilityToAdd = copiedPool.RandomElement();
                }

                money -= abilityToAdd.AbilityCostForRaid;
                var addedAbility = __result.PsiTracker().AddAbility(abilityToAdd);
                copiedPool.Remove(abilityToAdd);
                
                // Configure autocasting if autocast ability
                if (abilityToAdd.Autocastable && addedAbility != null) {
                    AutocastProfileUtility.SelectAndConfigureAutocastProfile(ref addedAbility);
                    addedAbility.Autocast = true;
                }

                // Force give capacitance so that they can use these abilities
                if ((abilityToAdd == PsiTechDefOf.PTPsiRally || abilityToAdd == PsiTechDefOf.PTPsiStorm) 
                    && copiedPool.Contains(PsiTechDefOf.PTPerfectedCapacitance)) {
                    __result.PsiTracker().AddAbility(PsiTechDefOf.PTPerfectedCapacitance, true);
                    money -= PsiTechDefOf.PTPerfectedCapacitance.AbilityCostForRaid;
                    copiedPool.Remove(PsiTechDefOf.PTPerfectedCapacitance);
                }
                
                // Clean up pool
                foreach (var ability in copiedPool.ListFullCopy()) {
                    if (ability.AbilityCostForRaid <= money &&
                        !__result.PsiTracker().Abilities.Any(existing => existing.Def.ConflictingAbilities.Contains(ability)) &&
                        __result.PsiTracker().HasAvailableSlot(ability.Tier)) continue;

                    copiedPool.Remove(ability);
                }
                
            }
            
            // Check if we need a psychic weapon
            if (__result.equipment.Primary != null && __result.GetStatValue(PsiTechDefOf.PTPsiWeaponSynchronicity) > 1e-4 &&
                !(__result.equipment.Primary.def.thingSetMakerTags?.Contains("SingleUseWeapon") ?? false) &&
                !(__result.equipment.Primary.def.thingCategories?.Contains(ThingCategoryDef.Named("Grenades")) ?? false)) {
                __result.equipment.Primary.PsiEquipmentTracker().IsPsychic = true;
            }
            
            // Max out our energy for the assault
            __result.PsiTracker().MaxEnergy();
            
        }
        
    }
    
    [HarmonyPatch(typeof(IncidentParmsUtility), "GetDefaultPawnGroupMakerParms")]
    public class RaidGroupParmsPatch {
        // You might ask yourself, "Why is this necessary?"
        // Well, it's necessary because Tynan didn't add a disallowedWeaponTags or way to disable single-use
        // rocket launchers per group. This is his fault.

        public static void Postfix(ref PawnGroupMakerParms __result) {
            if (__result.faction?.def != PsiTechDefOf.PTPsionic) return;

            __result.dontUseSingleUseRocketLaunchers = true;
        }
    
    }

    [HarmonyPatch(typeof(FactionDef), "RaidCommonalityFromPoints")]
    public class RaidFactionWeightPatch {
        // And now maybe you're asking yourself, "What the is this?"
        // Well, there's no way to set conditions on a faction being used for a raid, for some reason.

        public static float Postfix(float __result, FactionDef __instance) {
            if (__instance == PsiTechDefOf.PTPsionic && (!PsiTechSettings.Get().EnablePsychicFactionRaids ||
                                                         !PsiTechDefOf.PTProjectionTheory.IsFinished)) {
                return 0f;
            }

            return __result;
        }
    }

}