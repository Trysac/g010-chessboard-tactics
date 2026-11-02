using System;

namespace ChessboardTactics.Core
{
    /// <summary>The outcome of one attack, computed before it happens so the UI and the AI can preview it.</summary>
    public readonly struct AttackForecast
    {
        public AttackForecast(int damage, bool defenderFalls, int counterDamage, bool attackerFalls)
        {
            Damage = damage;
            DefenderFalls = defenderFalls;
            CounterDamage = counterDamage;
            AttackerFalls = attackerFalls;
        }

        /// <summary>Health the defender loses.</summary>
        public int Damage { get; }
        public bool DefenderFalls { get; }
        /// <summary>Health the attacker loses to the counter-attack (0 when there is none).</summary>
        public int CounterDamage { get; }
        public bool AttackerFalls { get; }
    }

    /// <summary>
    /// Deterministic combat: the attacker deals its full attack; a defender that survives and has the
    /// attacker inside its own range strikes back with half its attack (at least 1).
    /// </summary>
    public static class Combat
    {
        public static AttackForecast Forecast(Unit attacker, Coord attackFrom, Unit defender)
        {
            int damage = Math.Min(attacker.Stats.Attack, defender.Health);
            bool defenderFalls = damage >= defender.Health;
            int counter = 0;
            if (!defenderFalls && defender.Stats.InAttackRange(Coord.Chebyshev(attackFrom, defender.Position)))
            {
                counter = Math.Min(defender.Stats.CounterAttack, attacker.Health);
            }
            return new AttackForecast(damage, defenderFalls, counter, counter > 0 && counter >= attacker.Health);
        }

        public static AttackForecast Forecast(Unit attacker, Unit defender) => Forecast(attacker, attacker.Position, defender);

        internal static AttackForecast Resolve(Unit attacker, Unit defender)
        {
            AttackForecast forecast = Forecast(attacker, defender);
            defender.TakeDamage(forecast.Damage);
            attacker.TakeDamage(forecast.CounterDamage);
            return forecast;
        }
    }
}
