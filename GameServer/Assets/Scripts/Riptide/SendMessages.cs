using RiptideNetworking;
using UnityEngine;

public class SendMessages
{
    #region Messages
    /// <summary>Sends a player's updated position to all clients.</summary>
    /// <param name="_player">The player whose position to update.</param>
    public static void SetPosition(Player _player)
    {
        if (!_player)
            return;

        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.setPosition);
        
        message.Add(_player.id);
        message.Add(_player.transform.position);
        message.Add(_player.tick);

        NetworkManager.Singleton.Server.SendToAll(message, _player.id);
    }

    /// <summary>Sends a player's updated rotation to all clients except to himself (to avoid overwriting the local player's rotation).</summary>
    /// <param name="_player">The player whose rotation to update.</param>
    public static void SetRotation(Player _player)
    {
        if (!_player)
            return;

        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.setRotation);
        
        message.Add(_player.id);
        message.Add(_player.transform.rotation);
        message.Add(_player.tick);

        NetworkManager.Singleton.Server.SendToAll(message, _player.id);
    }

    /// <summary>Sends a player's updated position and rotation to all clients except to the client himself (to avoid overwriting the player's simulation state).</summary>
    /// <param name="_player">The player whose position and rotation to update.</param>
    public static void SetTransform(Player player)
    {
        if (!player)
            return;

        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.setTransform);

        message.Add(true);
        message.Add(player.id);
        message.Add(player.transform.position);
        message.Add(player.head.transform.rotation);
        message.Add(player.tick);

        NetworkManager.Singleton.Server.SendToAll(message, player.id);
    }

    /// <summary>Sends a enemys's updated position and rotation to all clients.</summary>
    /// <param name="enemy">The enemy whose position and rotation to update.</param>
    public static void SetTransform(Enemy enemy)
    {
        if (!enemy)
            return;

        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.setTransform);

        message.Add(false);
        message.Add(enemy.id);
        message.Add(enemy.transform.position);
        message.Add(enemy.transform.rotation);
        message.Add(NetworkManager.Singleton.tick);

        NetworkManager.Singleton.Server.SendToAll(message);
    }

    /// <summary>Sends a player's animation properties to all clients except to the client himself (to avoid overwriting the player's simulation state).</summary>
    /// <param name="_player">The player whose position and rotation to update.</param>
    public static void PlayerAnimation(Player _player)
    {
        if (!_player)
            return;

        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.playerAnimation);

        message.Add(_player.id);
        message.Add(_player.isFiring);
        message.Add(_player.lateralSpeed);
        message.Add(_player.forwardSpeed);
        message.Add(_player.isGrounded);
        message.Add(_player.jumping);

        NetworkManager.Singleton.Server.SendToAll(message, _player.id);
    }

    /// <summary>Sends a player's simulation state.</summary>
    /// <param name="_toClient">The client that should receive the simulation state.</param>
    /// <param name="_simulationState">The simulation state to send.</param>
    public static void SendSimulationState(ushort _toClient, SimulationState _simulationState)
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.serverSimulationState);

            message.Add(_simulationState.position);
            message.Add(_simulationState.velocity);
            message.Add(_simulationState.simulationFrame);

        NetworkManager.Singleton.Server.Send(message, _toClient);
    }

    /// <summary>Sends a convar state.</summary>
    /// <param name="i">The convar to send.</param>
    public static void SendConvar(Convar i)
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.serverConvar);

        message.Add(i.name);
        message.Add(i.value);
        message.Add(i.helpString);

        NetworkManager.Singleton.Server.SendToAll(message);
    }

    /// <summary>Sends current server tick.</summary>
    public static void ServerTick()
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.serverTick);

            message.Add(NetworkManager.Singleton.tick);

        NetworkManager.Singleton.Server.SendToAll(message);
    }

    #endregion
}
