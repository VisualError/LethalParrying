using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using BepInEx;
using System.Collections;
using System;
using System.Collections.Generic;

namespace LethalParrying.Patches
{
    [HarmonyPatch(typeof(Shovel))]
    class ShovelPatch
    {
        [HarmonyPatch(nameof(Shovel.HitShovel))]
        [HarmonyPostfix]
        public static void HitShovelPostfix(ref Shovel __instance, ref List<RaycastHit> ___objectsHitByShovelList, ref PlayerControllerB ___previousPlayerHeldBy)
        {
            if(___objectsHitByShovelList != null || ___objectsHitByShovelList.Count != 0 && LethalParryBase.stun && LethalParryBase.serverModCheck)
            {
                LethalParryBase.logger.LogInfo("Got hit list");
                EnemyAI enemyAI;
                foreach(RaycastHit hitObject in ___objectsHitByShovelList)
                {
                    if (hitObject.transform.parent.TryGetComponent(out enemyAI) && hitObject.transform != ___previousPlayerHeldBy)
                    {
                        if(enemyAI != null)
                        {
                            try
                            {
                                LethalParryBase.logger.LogInfo($"Stunned enemy {enemyAI.enemyType}!");
                                enemyAI.SetEnemyStunned(true, 1f, ___previousPlayerHeldBy);
                            }
                            catch (Exception arg)
                            {
                                Debug.Log(string.Format("Exception caught when hitting object with shovel from player #{0}: {1}", ___previousPlayerHeldBy.playerClientId, arg));
                            }
                        }
                    }
                }
                LethalParryBase.stun = false;
            }
        }
    }
}
