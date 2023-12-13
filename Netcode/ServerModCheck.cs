using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

namespace LethalParrying.Netcode
{
    [HarmonyPatch]
    public class ServerModCheck
    {
        public static PlayerControllerB localPlayer;
        public static bool hasMod = false;
        public static bool synced = false;
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void InitLocalPlayer(PlayerControllerB __instance)
        {
            localPlayer = __instance;
            if (NetworkManager.Singleton.IsServer)
            {
                hasMod = true;
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LethalParryingOnRequestModCheck", OnRequest);
                LethalParryBase.logger.LogInfo("Setting up Server CustomMessagingManager");
                SyncOnLocalClient();
            }
            else
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LethalParryingOnReceiveModCheck", OnReceive);
                LethalParryBase.logger.LogInfo("Setting up Client CustomMessagingManager");
                SendRequestToServer();
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
        [HarmonyPrefix]
        public static bool OnDestroy()
        {
            if(NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LethalParryingOnRequestModCheck");
                    LethalParryBase.logger.LogInfo("Destroying Server CustomMessagingManager");
                }
                else
                {
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LethalParryingOnReceiveModCheck");
                    LethalParryBase.logger.LogInfo("Destroying Client CustomMessagingManager");
                }
            }
            hasMod = false;
            LethalParryBase.serverModCheck = false;
            LethalParryBase.logger.LogInfo("Setting mod check to false");
            return true;
        }

        public static void SendRequestToServer()
        {
            if (NetworkManager.Singleton.IsClient)
            {
                LethalParryBase.logger.LogInfo("Sending request to server.");
                FastBufferWriter writer = new FastBufferWriter(4, Unity.Collections.Allocator.Temp);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LethalParryingOnRequestModCheck", NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
            else
            {
                LethalParryBase.logger.LogError("Faile to send request if server has mod.");
            }
        }

        public static void OnRequest(ulong clientId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                LethalParryBase.logger.LogInfo($"Player_ID: {clientId} Requested for Mod Check.");
                bool value = true;
                FastBufferWriter writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(value), Unity.Collections.Allocator.Temp);
                writer.WriteValueSafe(value);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LethalParryingOnReceiveModCheck", clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        
        public static void OnReceive(ulong clientId, FastBufferReader reader)
        {
            bool value;
            reader.ReadValueSafe(out value);
            hasMod = value;
            LethalParryBase.logger.LogInfo($"Received mod check: {hasMod} from server!");
            SyncOnLocalClient();
        }

        public static void SyncOnLocalClient()
        {
            LethalParryBase.serverModCheck = hasMod;
        }
    }
}
