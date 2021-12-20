using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Multiplayer
{
    /// <summary> Responsible for backtracking entities, recording their position </summary>
    public class LagCompensation
    {
        public static List<PlayerRecord>[] playerRecords = new List<PlayerRecord>[NetworkManager.Singleton.maxClientCount + 1];

        static Convar maxLagComp = new Convar("sv_maxunlag", 1, "Maximum time limit for Player records", Flags.SERVER, 0f, 5f);

        public static void Start(int maxPlayers)
        {
            //Initialize playerRecords
            playerRecords = new List<PlayerRecord>[maxPlayers + 1];
            for (int i = 0; i < playerRecords.Length; i++)
            {
                playerRecords[i] = new List<PlayerRecord>();
            }
        }

        public static void Stop()
        {
            // Clear playerRecords
            for (int i = 0; i < playerRecords.Length; i++)
            {
                playerRecords[i].Clear();
            }
        }

        // Backup, backtrack, do something and restore
        public static void Backtrack(ushort _client, int _tick, float _lerpAmount = 0)
        {
            if (!NetworkedEntity.playerList.TryGetValue(_client, out Player localPlayer))
                return;

            // Backtrack and backup the players
            PlayerRecord[] backup = new PlayerRecord[NetworkManager.Singleton.maxClientCount + 1];
            for (ushort i = 1; i <= NetworkManager.Singleton.maxClientCount; i++)
            {
                // Dont backtrack the player who requested the backtack
                if (i == _client)
                    continue;

                if (!NetworkedEntity.playerList.TryGetValue(i, out Player player))
                    continue;

                backup[i] = Backup(player);
                BacktrackPlayer(player, _tick, _lerpAmount);
            }

            // Do something


            // Restore
            for (ushort i = 1; i <= NetworkManager.Singleton.maxClientCount; i++)
            {
                if (i == _client)
                    continue;

                if (!NetworkedEntity.playerList.TryGetValue(i, out Player player) || backup[i] == new PlayerRecord())
                    continue;
                Restore(player, backup[i]);
            }
        }

        static PlayerRecord Backup(Player _player)
        {
            return new PlayerRecord(_player.transform.position, _player.transform.rotation, _player.tick);
        }

        static void Restore(Player _player, PlayerRecord backupRecord)
        {
            _player.transform.position = backupRecord.position;
            _player.transform.rotation = backupRecord.rotation;
        }

        public static void BacktrackPlayer(Player _player, int _tick, float _lerpAmount = 0)
        {
            int currentRecord = -1;

            // Loop through records and find the current one
            for (int i = 0; i < playerRecords[_player.id].Count; i++)
            {
                if (playerRecords[_player.id].ElementAt(i).playerTick == _tick)
                {
                    currentRecord = i;
                    break;
                }
            }

            // Record couldnt be found, so we cant backtrack the player
            // so get the closest to the tick
            if (currentRecord <= -1)
            {
                float minDifference = float.MaxValue;

                // Loop through records and find the closest smaller one
                for (int i = 0; i < playerRecords[_player.id].Count; i++)
                {
                    float currentDifference = Mathf.Abs(_tick - playerRecords[_player.id].ElementAt(i).playerTick);
                    if (minDifference > currentDifference)
                    {
                        currentRecord = i;
                        minDifference = currentDifference;
                    }
                }
            }

            // Record couldnt be found or the current record surpasses the amount of player records,
            // so we cant backtrack the player, return
            if (currentRecord <= -1 || currentRecord >= playerRecords[_player.id].Count)
                return;

            PlayerRecord record = playerRecords[_player.id].ElementAt(currentRecord);
            if (record == null)
                return;


            PlayerRecord backupRecord = new PlayerRecord(_player.transform.position, _player.transform.rotation, _player.tick);

            // There is no next record, so just use the current record values
            if (currentRecord + 1 >= playerRecords[_player.id].Count)
            {
                _player.transform.position = record.position;
                _player.transform.rotation = record.rotation;
                return;
            }

            PlayerRecord nextRecord = playerRecords[_player.id].ElementAt(currentRecord + 1);

            // Set player position and rotation
            _player.transform.position = Vector3.Lerp(record.position, nextRecord.position, _lerpAmount);
            _player.transform.rotation = Quaternion.Lerp(record.rotation, nextRecord.rotation, _lerpAmount);
        }

        // Adds record to list, with current position, rotation and tick (works as a timestamp)
        public static void AddPlayerRecord(Player _player)
        {
            if (playerRecords[_player.id] == null)
                playerRecords[_player.id] = new List<PlayerRecord>();

            playerRecords[_player.id].Add(new PlayerRecord(_player.transform.position, _player.transform.rotation, _player.tick));
        }

        // Adds new player records and deletes old ones
        public static void UpdatePlayerRecords()
        {
            for (ushort i = 1; i <= NetworkManager.Singleton.maxClientCount; i++)
            {
                if (!NetworkedEntity.playerList.TryGetValue(i, out Player player))
                    continue;

                // Add a record this tick
                AddPlayerRecord(player);
            }

            // Loop through every player
            for (ushort i = 1; i <= NetworkManager.Singleton.maxClientCount; i++)
            {
                // Player doesnt exist, so clear all records
                if (!NetworkedEntity.playerList.TryGetValue(i, out Player player))
                {
                    playerRecords[i].Clear();
                    continue;
                }

                // Loop through every record
                for (ushort j = 0; j < playerRecords[i].Count; j++)
                {
                    // Check if the playerRecord doesnt exist or if the element doesnt exist
                    if (playerRecords[i] == null || playerRecords[i].ElementAt(j) == null)
                        continue;

                    // Check difference with the server
                    if (NetworkManager.Singleton.tick - playerRecords[i].ElementAt(j).playerTick > Utils.timeToTicks(maxLagComp.GetValue()))
                    {
                        // Remove if the difference is to big
                        playerRecords[i].RemoveAt(j);
                    }
                }
            }
        }
    }
}