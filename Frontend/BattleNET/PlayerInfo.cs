using System;
using System.ComponentModel.DataAnnotations;

namespace BattleNET.Models
{
    /// <summary>
    /// Represents a player returned by the RCON "players" command.
    /// This class is immutable to ensure thread safety and data consistency.
    /// </summary>
    public sealed class PlayerInfo : IEquatable<PlayerInfo>
    {
        /// <summary>
        /// Gets the player's name.
        /// </summary>
        [Required(ErrorMessage = "Player name is required")]
        [StringLength(32, MinimumLength = 1, ErrorMessage = "Player name must be between 1 and 32 characters")]
        public string Name { get; }

        /// <summary>
        /// Gets the player's score.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Score must be non-negative")]
        public int Score { get; }

        /// <summary>
        /// Gets the player's ping in milliseconds.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Ping must be non-negative")]
        public int Ping { get; }

        /// <summary>
        /// Gets the player's unique identifier.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Player ID must be non-negative")]
        public int Id { get; }

        /// <summary>
        /// Gets the player's connection time.
        /// </summary>
        public DateTime ConnectedAt { get; }

        /// <summary>
        /// Gets the player's last activity time.
        /// </summary>
        public DateTime LastActivity { get; }

        /// <summary>
        /// Initializes a new instance of the PlayerInfo class.
        /// </summary>
        /// <param name="name">The player's name.</param>
        /// <param name="score">The player's score.</param>
        /// <param name="ping">The player's ping in milliseconds.</param>
        /// <param name="id">The player's unique identifier.</param>
        /// <param name="connectedAt">The player's connection time.</param>
        /// <param name="lastActivity">The player's last activity time.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when name is empty or invalid.</exception>
        public PlayerInfo(string name, int score, int ping, int id, DateTime connectedAt, DateTime lastActivity)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Player name cannot be empty or whitespace.", nameof(name));

            Score = score;
            Ping = ping;
            Id = id;
            ConnectedAt = connectedAt;
            LastActivity = lastActivity;
        }

        /// <summary>
        /// Updates the player's last activity time.
        /// </summary>
        /// <returns>A new PlayerInfo instance with updated last activity time.</returns>
        public PlayerInfo UpdateActivity()
        {
            return new PlayerInfo(Name, Score, Ping, Id, ConnectedAt, DateTime.UtcNow);
        }

        /// <summary>
        /// Updates the player's score.
        /// </summary>
        /// <param name="newScore">The new score value.</param>
        /// <returns>A new PlayerInfo instance with updated score.</returns>
        public PlayerInfo UpdateScore(int newScore)
        {
            return new PlayerInfo(Name, newScore, Ping, Id, ConnectedAt, LastActivity);
        }

        /// <summary>
        /// Updates the player's ping.
        /// </summary>
        /// <param name="newPing">The new ping value.</param>
        /// <returns>A new PlayerInfo instance with updated ping.</returns>
        public PlayerInfo UpdatePing(int newPing)
        {
            return new PlayerInfo(Name, Score, newPing, Id, ConnectedAt, LastActivity);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerInfo);
        }

        /// <summary>
        /// Determines whether the specified PlayerInfo is equal to the current PlayerInfo.
        /// </summary>
        /// <param name="other">The PlayerInfo to compare with the current PlayerInfo.</param>
        /// <returns>true if the specified PlayerInfo is equal to the current PlayerInfo; otherwise, false.</returns>
        public bool Equals(PlayerInfo? other)
        {
            if (other is null)
                return false;

            return Id == other.Id &&
                   Name == other.Name &&
                   Score == other.Score &&
                   Ping == other.Ping;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Score, Ping);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Player {Name} (ID: {Id}, Score: {Score}, Ping: {Ping}ms)";
        }
    }
}
