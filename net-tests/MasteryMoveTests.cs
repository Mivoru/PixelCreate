using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The moves at the ends of the weapon mastery branches, each with and without the move taken.</summary>
public class MasteryMoveTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string weapon, string? unlock, params Vector3[] foes)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var types = foes.Select(_ => "goblin_brute").ToArray();
        var camp = new EnemySpawn(new Vector3(0, 0, -60), 1f, foes.Length, Types: types);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 4 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10, Buttons.None);
        server.EnemiesFrozen = true;
        var piece = server.GiveGear(1, new GearPiece(ItemDef.Index(weapon), Rarity.Common, 1, Array.Empty<AffixRoll>()));
        Run(server, client, network, 12, Buttons.None, GearCommands.Toggle(piece));
        if (unlock is not null)
        {
            server.GrantMastery(1, unlock);
        }

        var feet = server.GetPlayer(1)!.Value.Position;
        for (var i = 0; i < foes.Length; i++)
        {
            server.Enemies[i].Position = feet + foes[i];
            server.Enemies[i].Facing = MathF.PI;
        }

        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Buttons buttons, Command command = Command.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons, 0, i == 0 ? command : Command.None);
        }
    }

    /// <summary>Holds a button for some ticks, lets go, and waits for what it set off to finish.</summary>
    private static void Press(GameServer server, ClientPrediction client, SimNetwork network, Buttons button, int hold)
    {
        Run(server, client, network, hold, button);
        Run(server, client, network, 120, Buttons.None);
    }

    private static int[] Health(GameServer server) => server.Enemies.Select(e => (int)e.Health).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PiercingArrow_HitsEveryEnemyInLine(bool taken)
    {
        var (server, client, network) = Start("hunters_longbow", taken ? "piercing_arrow" : null, new Vector3(0, 0, -7), new Vector3(0, 0, -11));
        var before = Health(server);
        Press(server, client, network, Buttons.Light, Tuning.FullDraw + 4);
        var after = Health(server);
        Assert.True(after[0] < before[0], "The first enemy in line is always hit.");
        Assert.Equal(taken, after[1] < before[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StunningBash_LeavesSpunEnemiesReeling(bool taken)
    {
        var (server, client, network) = Start("spiked_club", taken ? "stunning_bash" : null, new Vector3(0, 0, -1.5f));
        var foe = server.Enemies[0];
        var before = foe.Health;
        var spin = Tuning.Attack(AttackKind.ClubSpin);
        Run(server, client, network, 2, Buttons.Heavy);
        Run(server, client, network, spin.HitAt + 4, Buttons.None);
        Assert.True(foe.Health < before);
        Assert.Equal(taken, foe.Action == EnemyAction.Stagger);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shockwave_ReachesPastTheSpin(bool taken)
    {
        var (server, client, network) = Start("spiked_club", taken ? "shockwave" : null, new Vector3(4.5f, 0, 0));
        var before = Health(server);
        Press(server, client, network, Buttons.Heavy, 2);
        Assert.Equal(taken, Health(server)[0] < before[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChainLightning_LeapsFromTheFireballsVictim(bool taken)
    {
        var (server, client, network) = Start("bone_staff", taken ? "chain_lightning" : null, new Vector3(0, 0, -8), new Vector3(5, 0, -9));
        var before = Health(server);
        Press(server, client, network, Buttons.Light, 2);
        var after = Health(server);
        Assert.True(after[0] < before[0], "The fireball hits the enemy it was cast at.");
        Assert.Equal(taken, after[1] < before[1]);
    }

    private static readonly World Field = new(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());

    private static AttackKind Tick(ref PlayerState state, Buttons buttons, sbyte z = 0) =>
        Movement.Step(ref state, new InputCmd { MoveZ = z, Buttons = buttons }, Field);

    private static PlayerState Sliding(byte weapon, MasteryMoves moves)
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Weapon = weapon;
        state.Moves = (byte)moves;
        for (var i = 0; i < 40; i++)
        {
            Tick(ref state, Buttons.None, 127);
        }

        Tick(ref state, Buttons.Slide, 127);
        Assert.True(state.SlideTicks > 0);
        return state;
    }

    private static PlayerState Airborne(byte weapon, MasteryMoves moves)
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Weapon = weapon;
        state.Moves = (byte)moves;
        Tick(ref state, Buttons.Jump);
        for (var i = 0; i < 6; i++)
        {
            Tick(ref state, Buttons.None);
        }

        Assert.False(state.Grounded);
        return state;
    }

    /// <summary>Counts the blows that land over some ticks with no buttons held.</summary>
    private static int Blows(ref PlayerState state, AttackKind kind, int ticks)
    {
        var blows = 0;
        for (var i = 0; i < ticks; i++)
        {
            blows += Tick(ref state, Buttons.None) == kind ? 1 : 0;
        }

        return blows;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RisingSlash_ComesOutOfASlide(bool taken)
    {
        var state = Sliding(Weapons.Sword, taken ? MasteryMoves.RisingSlash : MasteryMoves.None);
        Tick(ref state, Buttons.Heavy);
        Assert.Equal(taken ? AttackKind.RisingSlash : AttackKind.Heavy, state.Attack);
        Assert.Equal(taken, state.Velocity.Y > 0f);
    }

    [Fact]
    public void RisingSlash_ThrowsTheFoeUp()
    {
        var (server, client, network) = Start("wooden_sword", "rising_slash", new Vector3(0, 0, -2.2f));
        var foe = server.Enemies[0];
        server.EnemiesFrozen = false;
        var highest = 0f;
        for (var i = 0; i < 70; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, i < 20 ? (sbyte)-127 : (sbyte)0, i == 20 ? Buttons.Slide : i == 21 ? Buttons.Heavy : Buttons.None);
            if (server.GetPlayer(1)!.Value.Attack != AttackKind.RisingSlash)
            {
                // Keep the foe just ahead of the player until the cut comes.
                var feet = server.GetPlayer(1)!.Value.Position;
                foe.Position = new Vector3(feet.X, 0, feet.Z - 4.5f);
            }

            highest = MathF.Max(highest, foe.Position.Y);
        }

        Assert.Equal(EnemyAction.Stagger, foe.Action);
        Assert.True(highest > 0.5f, $"The foe flies: {highest}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Whirlwind_SpinsInTheAir(bool taken)
    {
        var state = Airborne(Weapons.Sword, taken ? MasteryMoves.Whirlwind : MasteryMoves.None);
        Tick(ref state, Buttons.Heavy);
        Assert.Equal(taken ? AttackKind.Whirlwind : AttackKind.Heavy, state.Attack);
        if (taken)
        {
            Assert.Equal(4, Blows(ref state, AttackKind.Whirlwind, 70));
        }
    }

    [Fact]
    public void GroundPound_LandsWhereThePlungeMeetsTheGround()
    {
        var state = Airborne(Weapons.Club, MasteryMoves.GroundPound);
        Tick(ref state, Buttons.Heavy);
        Assert.Equal(AttackKind.GroundPound, state.Attack);
        var landedAt = -1;
        for (var i = 0; i < 120 && landedAt < 0; i++)
        {
            if (Tick(ref state, Buttons.None) == AttackKind.GroundPound)
            {
                landedAt = i;
            }
        }

        Assert.InRange(landedAt, 1, 40);
        Assert.True(state.Grounded);
        Assert.Equal(0, Blows(ref state, AttackKind.GroundPound, 30));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BladeDance_ChainsFiveStabsOutOfASlide(bool taken)
    {
        var state = Sliding(Weapons.Dagger, taken ? MasteryMoves.BladeDance : MasteryMoves.None);
        Tick(ref state, Buttons.Light);
        var kind = state.Attack;
        Assert.Equal(taken ? AttackKind.BladeDance : AttackKind.DaggerFlurry, kind);
        var start = state.Position;
        Assert.Equal(taken ? 5 : 2, Blows(ref state, kind, 60));
        if (taken)
        {
            Assert.True(Vector3.Distance(start, state.Position) > 3f);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShadowStep_PutsThePlayerBehindTheEnemy(bool taken)
    {
        var (server, client, network) = Start("twin_daggers", taken ? "shadow_step" : null, new Vector3(0, 0, -6));
        var foe = server.Enemies[0];
        Run(server, client, network, 1, Buttons.Jump);
        Run(server, client, network, 6, Buttons.None);
        Run(server, client, network, 1, Buttons.Heavy);
        Run(server, client, network, 6, Buttons.None);
        var player = server.GetPlayer(1)!.Value.Position;
        Assert.Equal(taken, player.Z < foe.Position.Z);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, Tuning.FanKnives)]
    public void FanOfKnives_ThrowsARingWhenTheLeapLands(bool taken, int knives)
    {
        var (server, client, network) = Start("twin_daggers", taken ? "fan_of_knives" : null, new Vector3(0, 0, -40));
        Run(server, client, network, 1, Buttons.Heavy);
        var most = 0;
        for (var i = 0; i < Tuning.LeapHitAt + 10; i++)
        {
            Run(server, client, network, 1, Buttons.None);
            most = Math.Max(most, server.ProjectileCount);
        }

        Assert.Equal(knives, most);
    }

    private static int WallDamage(bool meteor)
    {
        var (server, client, network) = Start("bone_staff", meteor ? "meteor" : null, new Vector3(0, 0, -9.8f));
        var before = server.Enemies[0].Health;
        Press(server, client, network, Buttons.Heavy, 2);
        return before - server.Enemies[0].Health;
    }

    [Fact]
    public void Meteor_FallsOnTheEndOfTheLineOfFire()
    {
        var plain = WallDamage(false);
        var withMeteor = WallDamage(true);
        Assert.True(withMeteor > plain, $"With meteor {withMeteor}, without {plain}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RainOfArrows_FallsWhereASteepShotLands(bool taken)
    {
        var (server, client, network) = Start("hunters_longbow", taken ? "rain_of_arrows" : null, new Vector3(0, 0, -40));
        var pitch = (short)MathF.Round(0.9f / MathF.Tau * 65536f);
        var most = 0;
        for (var tick = 0; tick < Tuning.FullDraw + 240; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, tick < Tuning.FullDraw + 4 ? Buttons.Light : Buttons.None, 0, Command.None, 0, pitch);
            most = Math.Max(most, server.ProjectileCount);
        }

        Assert.Equal(taken, most > 1);
    }

    private static (int Lost, int Barrier) Slammed(bool barrier)
    {
        var (server, client, network) = Start("bone_staff", barrier ? "arcane_barrier" : null, new Vector3(0, 0, -2));
        Run(server, client, network, Tuning.BarrierDelayTicks + (int)(Tuning.BarrierFillSeconds * Tuning.TickRate) + 30, Buttons.None);
        var shield = client.Latest!.Barrier;
        var foe = server.Enemies[0];
        var feet = server.GetPlayer(1)!.Value.Position;
        var health = server.GetPlayer(1)!.Value.Health;
        foe.Position = feet + new Vector3(0, 0, -2f);
        foe.LeapTarget = feet;
        ((IEnemyEvents)server).Fire(foe, foe.Def.Abilities!.First(a => a.Effect == "slam"), new Target(1, feet, Vector3.Zero));
        return (health - server.GetPlayer(1)!.Value.Health, shield);
    }

    [Fact]
    public void ArcaneBarrier_SoaksUpBlows()
    {
        var (open, none) = Slammed(false);
        var (shielded, shield) = Slammed(true);
        Assert.Equal(0, none);
        Assert.True(shield > 20, $"Barrier {shield}.");
        Assert.True(shielded < open, $"Shielded lost {shielded}, open lost {open}.");
    }
}
