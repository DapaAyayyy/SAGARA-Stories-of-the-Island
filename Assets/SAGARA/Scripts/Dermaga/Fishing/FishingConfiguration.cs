using System;
using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    public enum FishRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum FishingDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum FishingState
    {
        Idle,
        Casting,
        WaitingForBite,
        BiteDetected,
        Minigame,
        CatchSuccess,
        CatchFail,
        Cleanup
    }

    [Serializable]
    public sealed class RaritySettings
    {
        // Catch identity and requirements.
        public FishRarity rarity;
        public string fishId;
        public float weight;
        public int minimumScore;

        // Rarity feedback.
        public Color aura;
        public float intensity = 1;

        public RaritySettings(FishRarity r, float w, int score, Color c)
        {
            rarity = r;
            weight = w;
            minimumScore = score;

            aura = c;
            fishId = "prototype_fish_" + r.ToString().ToLowerInvariant();
            intensity = 1 + (int)r * .3f;
        }
    }

    [CreateAssetMenu(menuName = "SAGARA/Dermaga/Fishing configuration")]
    public sealed class FishingConfiguration : ScriptableObject
    {
        // Rarity table.
        public RaritySettings[] rarities =
        {
            new RaritySettings(FishRarity.Common, 40, 50, Color.white),
            new RaritySettings(FishRarity.Uncommon, 30, 60, new Color(.3f, .85f, .4f)),
            new RaritySettings(FishRarity.Rare, 15, 70, new Color(.2f, .55f, 1)),
            new RaritySettings(FishRarity.Epic, 10, 80, new Color(.7f, .35f, 1)),
            new RaritySettings(FishRarity.Legendary, 5, 90, new Color(1, .75f, .15f))
        };

        // Phase and input timing.
        [Min(.1f)]
        public float castSeconds = 1.0f;
        public Vector2 biteWaitSeconds = new Vector2(2, 4);
        [Min(.1f)]
        public float biteNoticeSeconds = .8f;
        [Min(.1f)]
        public float arrowSeconds = 1.8f;
        [Min(.01f)]
        public float inputInterval = .12f;
        [Min(.1f)]
        public float resultSeconds = 2.5f;

        // Deterministic selection settings.
        public bool deterministicSeed;
        public int seed = 808;

        public bool IsValid
        {
            get
            {
                if (rarities == null || rarities.Length != 5)
                {
                    return false;
                }

                float total = 0;

                for (int i = 0; i < 5; i++)
                {
                    var r = rarities[i];

                    if (r == null
                        || (int)r.rarity != i
                        || r.weight < 0
                        || float.IsNaN(r.weight)
                        || float.IsInfinity(r.weight)
                        || r.minimumScore < 0
                        || r.minimumScore > 100)
                    {
                        return false;
                    }

                    total += r.weight;
                }

                return total > 0;
            }
        }
    }

    public static class FishingRules
    {
        // Round dimensions and scoring.
        public const int Combinations = 5;
        public const int ArrowsPerCombination = 4;
        public const int Slots = 20;
        public const int Points = 5;

        public static int Roll(System.Random random, RaritySettings[] settings)
        {
            double total = 0;

            foreach (var r in settings)
            {
                total += Math.Max(0, r.weight);
            }

            if (total <= 0)
            {
                throw new ArgumentException("Rarity weights must have positive total");
            }

            double roll = random.NextDouble() * total;
            int last = 0;

            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i].weight <= 0)
                {
                    continue;
                }

                last = i;
                roll -= settings[i].weight;

                if (roll < 0)
                {
                    return i;
                }
            }

            return last;
        }

        public static FishingDirection[] Sequence(System.Random random)
        {
            var a = new FishingDirection[Slots];

            for (int i = 0; i < a.Length; i++)
            {
                a[i] = (FishingDirection)random.Next(4);
            }

            return a;
        }

        public static bool Succeeds(int score, int minimum)
        {
            return score >= minimum;
        }
    }

    public sealed class FishingRound
    {
        // Sequence and recorded outcomes.
        public readonly FishingDirection[] sequence;
        public readonly sbyte[] outcomes = new sbyte[20];

        public int Cursor { get; private set; }

        public int Score { get; private set; }

        public bool Complete => Cursor == 20;

        public FishingRound(FishingDirection[] directions)
        {
            if (directions == null || directions.Length != 20)
            {
                throw new ArgumentException("Exactly 20 slots required");
            }

            sequence = (FishingDirection[])directions.Clone();
        }

        public bool Consume(FishingDirection? direction)
        {
            if (Complete)
            {
                return false;
            }

            bool correct = direction.HasValue && direction.Value == sequence[Cursor];
            outcomes[Cursor] = (sbyte)(correct ? 1 : -1);

            if (correct)
            {
                Score += 5;
            }

            Cursor++;
            return correct;
        }
    }

    [Serializable]
    public sealed class FishingResult
    {
        // Catch identity.
        public string fishId;
        public string spotId;
        public FishRarity rarity;

        // Recorded score and result.
        public int score;
        public int requiredScore;
        public bool success;

        public FishingResult(string fish, FishRarity r, int points, int required, string spot)
        {
            fishId = fish;
            rarity = r;
            score = points;
            requiredScore = required;
            spotId = spot;
            success = FishingRules.Succeeds(points, required);
        }
    }
}
