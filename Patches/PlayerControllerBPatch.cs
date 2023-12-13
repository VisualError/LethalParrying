using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using BepInEx;
using System.Collections;
using System;

namespace LethalParrying.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    class PlayerControllerBPatch
    {
        internal static bool isPerfectParryFrame = false;
        internal static bool isSlow = false;
        internal static float perfectParryWindow = 0.25f;
        internal static float lastParryTime;
        internal static float perfectParryCooldown = 2f;
        internal static GrabbableObject currentItem;
        internal static Shovel shovel = null;
        internal static bool ParriedDeath = false;
        internal static Coroutine currentCoroutine;
        //static LayerMask enemyLayer = LayerMask.GetMask("Enemies");
        //static ServerAudio audio = new ServerAudio();
        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer))]
        [HarmonyPrefix]
        static bool KillPlayerPatch(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            if (!LethalParryBase.serverModCheck)
            {
                return true;
            }
            if (!__instance.IsOwner)
            {
                return false;
            }
            if (__instance.isPlayerDead)
            {
                return false;
            }
            if (!__instance.AllowPlayerDeath())
            {
                return false;
            }
            if (ParriedDeath)
            {
                LethalParryBase.logger.LogWarning("Server called KillPlayer method again. Might have to patch this method properly.");
                ParriedDeath = false;
                return false;
            }
            if (causeOfDeath == CauseOfDeath.Unknown || causeOfDeath == CauseOfDeath.Gravity || causeOfDeath == CauseOfDeath.Abandoned || causeOfDeath == CauseOfDeath.Suffocation  || causeOfDeath == CauseOfDeath.Drowning || shovel == null)
            {
                return true;
            }
            if (isPerfectParryFrame)
            {
                if (LethalParryBase.Notify.Value) { HUDManager.Instance.DisplayTip("Parried Death..", "Nothing can stop you.", true); }
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
                __instance.DropAllHeldItems(true);
                lastParryTime = 0;
                ParriedDeath = true;
                return false;
            }
            return true;
        }
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPrefix]
        static bool DamagePlayerPatch(PlayerControllerB __instance, ref int damageNumber, CauseOfDeath causeOfDeath)
        {
            if (!LethalParryBase.serverModCheck)
            {
                return true;
            }
            if (!__instance.IsOwner)
            {
                return false;
            }
            if (__instance.isPlayerDead)
            {
                return false;
            }
            if (!__instance.AllowPlayerDeath())
            {
                return false;
            }
            if (causeOfDeath == CauseOfDeath.Abandoned || causeOfDeath == CauseOfDeath.Suffocation || causeOfDeath == CauseOfDeath.Drowning || shovel == null)
            {
                return true;
            }
            if (isPerfectParryFrame)
            {
                isSlow = false;
                if (currentCoroutine == null)
                {
                    currentCoroutine = __instance.StartCoroutine(DoHit(__instance, shovel));
                }
                MakeCriticallyInjured(__instance, false);
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                if (LethalParryBase.Notify.Value) { HUDManager.Instance.DisplayTip("Parried!", "Nice job!"); }
                __instance.ResetFallGravity();
                lastParryTime = 0;
            }
            else if(Keyboard.current.fKey.isPressed)
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

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePatch(PlayerControllerB __instance)
        {
            if (!LethalParryBase.serverModCheck)
            {
                return;
            }
            if ((__instance.IsOwner && __instance.isPlayerControlled && (!__instance.IsServer || __instance.isHostPlayerObject)) || __instance.isTestingPlayer)
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
                        MakeCriticallyInjured(__instance, false);
                    }
                } // Need to run this because I suck at programming.
                if (currentItem == null) { return; }
                shovel = (currentItem is Shovel) ? currentItem as Shovel : null;
                if (shovel == null)
                {
                    return;
                }
                if ((Keyboard.current.fKey).wasPressedThisFrame)
                {
                    if(shovel.reelingUp || __instance.bleedingHeavily)
                    {
                        if(LethalParryBase.Notify.Value) { HUDManager.Instance.DisplayTip("Can't parry.", $"You are attacking or injured."); }
                        return;
                    }
                    if (!IsParryOnCooldown())
                    {
                        __instance.StartCoroutine(PerfectParryWindow(__instance,shovel));
                        MakeCriticallyInjured(__instance, true);
                        LethalParryBase.stun = true;
                        isSlow = true;
                    }
                    else if (!isPerfectParryFrame && LethalParryBase.DisplayCooldown.Value)
                    {
                        HUDManager.Instance.DisplayTip("Can't parry. On cooldown", $"Can parry again after {Math.Round(perfectParryCooldown - Time.time + lastParryTime, 1)} seconds.");
                    }
                }
            }
        }

        // All Non-Patch related functions here.
        static IEnumerator DoHit(PlayerControllerB player, Shovel shovel)
        {
            AccessTools.Field(typeof(Shovel), "previousPlayerHeldBy").SetValue(shovel, player); // Fixes bugs related to negative cooldowns.
            shovel.reelingUp = true;
            if (currentCoroutine != null)
            {
                yield break;
            }
            player.activatingItem = true;
            player.twoHanded = true;
            player.playerBodyAnimator.ResetTrigger("shovelHit");
            player.playerBodyAnimator.SetBool("reelingUp", true);
            shovel.shovelAudio.PlayOneShot(shovel.reelUp);
            yield return new WaitForSeconds(0.1f);
            shovel?.SwingShovel(!shovel.isHeld);
            yield return new WaitForSeconds(0.15f);
            shovel?.HitShovel(!shovel.isHeld);
            shovel.reelingUp = false;
            AccessTools.Field(typeof(Shovel), "reelingUpCoroutine").SetValue(shovel, null);
            currentCoroutine = null;
            yield break;
        }
        static IEnumerator PerfectParryWindow(PlayerControllerB player,Shovel shovel)
        {
            if (isPerfectParryFrame)
            {
                yield break;
            }
            isPerfectParryFrame = true;
            lastParryTime = Time.fixedTime;
            shovel.shovelAudio.PlayOneShot(shovel.reelUp);
            shovel.ReelUpSFXServerRpc();
            yield return new WaitForSeconds(perfectParryWindow);
            isPerfectParryFrame = false;
        }

        static void MakeCriticallyInjured(PlayerControllerB player,bool enable)
        {
            player.criticallyInjured = enable;
            player.playerBodyAnimator.SetBool("Limp", enable);
        }

        static bool IsParryOnCooldown()
        {
            // Check if enough time has passed since the last parry
            return Time.fixedTime - lastParryTime < perfectParryCooldown;
        }
    }
}
