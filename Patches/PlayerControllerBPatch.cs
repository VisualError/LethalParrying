using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using BepInEx;
using System.Collections;
using System;

namespace LethalParrying.Patches
{
    // Welcome to
    [HarmonyPatch(typeof(PlayerControllerB))]
    class PlayerControllerBPatch
    {
        static bool isPerfectParryFrame = false;
        static bool isSlow = false;
        static float perfectParryWindow = 0.27f;
        static float lastParryTime;
        static float perfectParryCooldown = 2f;
        static GrabbableObject currentItem;
        static Shovel shovel = null;
        //static LayerMask enemyLayer = LayerMask.GetMask("Enemies");
        //static ServerAudio audio = new ServerAudio();
        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer))]
        [HarmonyPrefix]
        static bool KillPlayerPatch(ref PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            if(causeOfDeath == CauseOfDeath.Unknown || causeOfDeath == CauseOfDeath.Gravity || causeOfDeath == CauseOfDeath.Abandoned || causeOfDeath == CauseOfDeath.Suffocation  || causeOfDeath == CauseOfDeath.Drowning || shovel == null)
            {
                return true;
            }
            if (isPerfectParryFrame)
            {
                if (LethalParryBase.Notify.Value) { HUDManager.Instance.DisplayTip("Parried Death..", "Nothing can stop you.", true); }
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
                __instance.DropAllHeldItems(true);
                lastParryTime = 0;
                return false;
            }
            return true;
        }
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPrefix]
        static bool DamagePlayerPatch(ref PlayerControllerB __instance, ref int damageNumber, CauseOfDeath causeOfDeath)
        {
            if (causeOfDeath == CauseOfDeath.Abandoned || causeOfDeath == CauseOfDeath.Suffocation || causeOfDeath == CauseOfDeath.Drowning || shovel == null)
            {
                return true;
            }
            if (isPerfectParryFrame)
            {
                isSlow = false;
                __instance.StartCoroutine(DoHit(__instance, shovel));
                __instance.MakeCriticallyInjured(false);
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                if (LethalParryBase.Notify.Value) { HUDManager.Instance.DisplayTip("Parried!", "Nice job!"); }
                __instance.ResetFallGravity();
                lastParryTime = 0;
            }else if(Keyboard.current.fKey.isPressed)
            {
                int negatedDamage = UnityEngine.Random.Range(0, damageNumber - UnityEngine.Random.Range(0, damageNumber));
                damageNumber -= negatedDamage;
                if (LethalParryBase.Notify.Value) { HUDManager.Instance.DisplayTip("Blocked Damage", $"Damage negated: {negatedDamage}"); }
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                int rnd = UnityEngine.Random.Range(1, 100);
                if(rnd <= LethalParryBase.DropProbability.Value)
                {
                    if(currentItem != null)
                    {
                        __instance.DiscardHeldObject(false, null, default, true);
                    }
                }
                //GlobalEffects.Instance.PlayAudioServer();
            }
            return !isPerfectParryFrame;
        }

        static IEnumerator DoHit(PlayerControllerB player, Shovel shovel)
        {
            shovel.reelingUp = true;
            player.activatingItem = true;
            player.twoHanded = true;
            player.playerBodyAnimator.ResetTrigger("shovelHit");
            player.playerBodyAnimator.SetBool("reelingUp", true);
            shovel.shovelAudio.PlayOneShot(shovel.reelUp);
            yield return new WaitForSeconds(0.1f);
            shovel.SwingShovel();
            yield return new WaitForSeconds(0.13f);
            shovel.HitShovel();
            yield return new WaitForSeconds(0.3f);
            shovel.reelingUp = false;
            AccessTools.Field(typeof(Shovel), "reelingUpCoroutine").SetValue(shovel, null);
            yield break;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePatch(ref PlayerControllerB __instance)
        {
            /*if (isPerfectParryFrame)
            {
                Collider[] hitEnemies = Physics.OverlapSphere(__instance.transform.position + Vector3.up, 5f, enemyLayer);
                if (hitEnemies.Length > 0)
                {
                    foreach(Collider other in hitEnemies)
                    {
                        EnemyAI enemy = other.transform.GetComponent<EnemyAI>();
                        if (!enemy || enemy.targetPlayer != __instance) { continue; }
                        enemy.SetEnemyStunned(true, 1, __instance);
                        HUDManager.Instance.DisplayTip("IT HAPPENED OOMGGG", "YOOO");
                    }
                }
            }*/ // no idea why this isn't working. boowmp :(

            currentItem = __instance.ItemSlots[__instance.currentItemSlot];
            if (!Keyboard.current.fKey.isPressed || currentItem == null || shovel == null)
            {
                if (__instance.bleedingHeavily)
                {
                    isSlow = false;
                    return;
                }
                if (isSlow && __instance.criticallyInjured)
                {
                    isSlow = false;
                    __instance.MakeCriticallyInjured(false);
                }
            } // Need to run this because I suck at programming.
            if (currentItem == null) { return; }
            shovel = (currentItem is Shovel) ? currentItem as Shovel : null;
            if (shovel == null)
            {
                return;
            }
            currentItem.currentUseCooldown = isSlow ? 10000 : 0; // If Using Shovel. Don't be able to parry, if is pressing parry button don't be able to use shovel. Simple.
            if ((Keyboard.current.fKey).wasPressedThisFrame)
            {
                if (!IsParryOnCooldown() && !shovel.reelingUp && !__instance.criticallyInjured)
                {
                    __instance.StartCoroutine(PerfectParryWindow(__instance, shovel));
                    __instance.MakeCriticallyInjured(true);
                    __instance.bleedingHeavily = false;
                    isSlow = true;
                }
                else if(!isPerfectParryFrame && LethalParryBase.DisplayCooldown.Value)
                {
                    HUDManager.Instance.DisplayTip("Can't parry. On cooldown", $"Can parry again after {Math.Round(perfectParryCooldown - Time.time + lastParryTime, 1)} seconds.");
                }
            }
        }
        static IEnumerator PerfectParryWindow(PlayerControllerB player, Shovel shovel)
        {
            if (isPerfectParryFrame)
            {
                yield break;
            }
            isPerfectParryFrame = true;
            lastParryTime = Time.time;
            shovel.shovelAudio.PlayOneShot(shovel.reelUp);
            shovel.ReelUpSFXServerRpc();
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForSeconds(perfectParryWindow-0.1f);
            isPerfectParryFrame = false;
        }
        static bool IsParryOnCooldown()
        {
            // Check if enough time has passed since the last parry
            return Time.time - lastParryTime < perfectParryCooldown;
        }
    }
}
