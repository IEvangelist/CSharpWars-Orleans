﻿using CSharpWars.Common.Helpers;
using CSharpWars.Helpers;
using CSharpWars.Orleans.Contracts.Player;
using Orleans;
using Orleans.Runtime;

namespace CSharpWars.Orleans.Grains;


public class PlayerState
{
    public bool Exists { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string PasswordSalt { get; set; }
}

public interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerDto> Login(string username, string password);
}

public class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPasswordHashHelper _passwordHashHelper;
    private readonly IJwtHelper _jwtHelper;
    private readonly IPersistentState<PlayerState> _state;

    public PlayerGrain(
        IPasswordHashHelper passwordHashHelper,
        IJwtHelper jwtHelper,
        [PersistentState("player", "playerStore")] IPersistentState<PlayerState> state)
    {
        _passwordHashHelper = passwordHashHelper;
        _jwtHelper = jwtHelper;
        _state = state;
    }

    public async Task<PlayerDto> Login(string username, string password)
    {
        if (!_state.State.Exists)
        {
            (var salt, var hashed) = _passwordHashHelper.CalculateHash(password);

            _state.State.Exists = true;
            _state.State.Username = username;
            _state.State.PasswordSalt = salt;
            _state.State.PasswordHash = hashed;
        }
        else
        {
            (_, var hashed) = _passwordHashHelper.CalculateHash(password, _state.State.PasswordSalt);

            if (_state.State.PasswordHash != hashed)
            {
                throw new ArgumentException("Invalid username and password", nameof(password));
            }
        }

        await _state.WriteStateAsync();

        var token = _jwtHelper.GenerateToken(username);
        return new PlayerDto(username, token);
    }
}