using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Tests;

public class WulverTreeTests
{
    private static DialogueGraph Graph => WulverDialogue.Graph;

    [Fact]
    public void TheTreeValidates() => Assert.Empty(DialogueValidator.Validate(Graph));

    [Fact]
    public void TheTreeHasThirtyToFortyNodesAndFewChoicesEach()
    {
        Assert.InRange(Graph.Nodes.Count, 30, 40);
        foreach (var n in Graph.Nodes) Assert.InRange(n.Choices.Count, 1, 5);
        Assert.InRange(Graph.Nodes.Max(n => n.Choices.Count), 3, 4);
        Assert.Equal(Graph.Nodes.Count, Graph.Nodes.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public void ANodeShowsNoMoreThanFourChoicesInAnyState()
    {
        foreach (var (_, runner) in Explore(Contexts()))
            Assert.InRange(runner.Choices.Count, 1, 4);
    }

    [Fact]
    public void EveryNodeIsReachedInSomeContext()
    {
        var seen = new HashSet<string>();
        foreach (var (_, runner) in Explore(Contexts())) seen.Add(runner.NodeId);
        Assert.DoesNotContain(Graph.Nodes.Select(n => n.Id), id => !seen.Contains(id));
    }

    [Fact]
    public void AtLeastFourFlagsChangeWhatHeSaysLater()
    {
        // A flag matters if some node's text differs with it set and unset (checked on every node, in a neutral context).
        var matters = new List<string>();
        foreach (var flag in new[] { WulverDialogue.HeardYew, WulverDialogue.AskedWhoIAm, WulverDialogue.GaveFishBack, WulverDialogue.KeptFish, WulverDialogue.ClaimedMaker })
        {
            bool changes = false;
            foreach (var node in Graph.Nodes)
            {
                string Text(bool on)
                {
                    var g = new DialogueGraph(Graph.Nodes, [new DialogueEntry(DialogueCondition.Always, node.Id)]);
                    var ctx = new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 2, Flags = on ? new HashSet<string> { flag } : new HashSet<string>() };
                    var r = new DialogueRunner(g, ctx);
                    return r.Text + "|" + string.Join("/", r.Choices.Select(c => c.Text));
                }
                if (Text(true) != Text(false)) changes = true;
            }
            // The maker claim changes entries rather than text, so also compare where a return visit begins.
            if (flag == WulverDialogue.ClaimedMaker)
            {
                string Start(bool on) => Graph.EntryFor(new DialogueContext { VisitCount = 2, EndingReached = true, Flags = on ? new HashSet<string> { flag } : new HashSet<string>() }) ?? "";
                changes |= Start(true) != Start(false);
            }
            if (changes) matters.Add(flag);
        }
        Assert.True(matters.Count >= 4, string.Join(", ", matters));
    }

    // ---- entry selection ----

    private static string Entry(DialogueContext c) => Graph.EntryFor(c)!;

    [Fact]
    public void EntriesDependOnWhoIsStandingThere()
    {
        Assert.Equal("first_intro", Entry(new DialogueContext()));
        Assert.Equal("ret_welcome", Entry(new DialogueContext { VisitCount = 1 }));
        Assert.Equal("ret_hurt", Entry(new DialogueContext { VisitCount = 1, HealthFraction = 0.2 }));
        Assert.Equal("ret_dead", Entry(new DialogueContext { VisitCount = 1, Deaths = 3 }));
        Assert.Equal("ret_key", Entry(new DialogueContext { VisitCount = 1, HasKey = true }));
        Assert.Equal("ret_ending", Entry(new DialogueContext { VisitCount = 1, EndingReached = true }));
        Assert.Equal("ret_deep", Entry(new DialogueContext { VisitCount = 1, DeepestFloor = 2, FloorCount = 4 }));
        Assert.Equal("ret_notes", Entry(new DialogueContext { VisitCount = 1, NotesRead = 3 }));
    }

    [Fact]
    public void HurtComesBeforeEverythingElseOnAReturnVisit()
    {
        var all = new DialogueContext { VisitCount = 1, HealthFraction = 0.1, Deaths = 9, HasKey = true, EndingReached = true, DeepestFloor = 3, FloorCount = 4, NotesRead = 9 };
        Assert.Equal("ret_hurt", Entry(all));
        Assert.Equal("ret_dead", Entry(all with { HealthFraction = 1 }));
    }

    // ---- the first meeting and the gift ----

    private static void Pick(DialogueRunner r, string text)
    {
        int i = r.Choices.ToList().FindIndex(c => c.Text.StartsWith(text));
        Assert.True(i >= 0, $"no choice '{text}' at {r.NodeId}; have {string.Join(" | ", r.Choices.Select(c => c.Text))}");
        Assert.True(r.Advance(i));
    }

    /// <summary>Takes a choice through the encounter, so gifts are handed over and what is remembered is kept.</summary>
    private static void Say(WulverEncounter e, PlayerVitals v, Perks p, string text)
    {
        var runner = e.Conversation!;
        int i = runner.Choices.ToList().FindIndex(c => c.Text.StartsWith(text));
        Assert.True(i >= 0, $"no choice '{text}' at {runner.NodeId}; have {string.Join(" | ", runner.Choices.Select(c => c.Text))}");
        Assert.True(e.Choose(i, v, p));
    }

    private static readonly WulverSpot Spot = new(10, 10, 60, 10.5, 61, 10.5, 0, 0, 0, 60);
    private static readonly WulverFacts Calm = new(0, 0, -1, 3, 0, false, false);

    [Fact]
    public void TheFirstMeetingIntroducesHimAndOffersTheGiftWhichComesOnceAPerRun()
    {
        var e = new WulverEncounter(1, "voxel", Spot);
        var vitals = new PlayerVitals();
        var perks = new Perks();
        vitals.Damage(50);

        var r = e.Begin(Calm, vitals, perks);
        Assert.Equal("first_intro", r.NodeId);
        Assert.Contains(WulverDialogue.Name, r.Text);
        Assert.Equal(1, e.Visits);
        Say(e, vitals, perks, "What is this place");
        Say(e, vitals, perks, "You mentioned a gift");
        Assert.Equal("gift_offer", r.NodeId);
        Say(e, vitals, perks, "Take it");
        Assert.True(e.GiftTaken);
        Assert.Equal(PlayerVitals.MaxHealth, vitals.Health);
        Assert.Equal(0, vitals.HardDamage);
        Assert.True(perks.ParryWindowBonus > 0);
        e.End();

        // Hurt again, return: the gift is not offered or given a second time.
        vitals.Damage(60);
        perks.Clear();
        var again = e.Begin(Calm, vitals, perks);
        Assert.DoesNotContain(again.Choices, c => c.Text.StartsWith("About that gift"));
        Assert.Equal(PlayerVitals.MaxHealth - 60, vitals.Health);
        Assert.Equal(0, perks.ParryWindowBonus);
    }

    [Fact]
    public void SomeoneBadlyHurtIsGivenTheGiftTheGentlerWay()
    {
        var e = new WulverEncounter(1, "voxel", Spot);
        var vitals = new PlayerVitals();
        vitals.Damage(80);
        var perks = new Perks();
        var r = e.Begin(Calm, vitals, perks);
        Say(e, vitals, perks, "What is this place");
        Say(e, vitals, perks, "You mentioned a gift");
        Assert.Contains(r.Choices, c => c.Text.StartsWith("Take it gladly"));
        Assert.DoesNotContain(r.Choices, c => c.Text == "Take it.");
        Say(e, vitals, perks, "Take it gladly");
        Assert.Equal("gift_take_hurt", r.NodeId);
        Assert.Equal(60, WulverEncounter.HurtGift.Seconds);
        Assert.True(perks.ParryWindowBonus >= 0.28 - 1e-9);
    }

    [Fact]
    public void HeGivesNothingToTheDeadButStillTalks()
    {
        var e = new WulverEncounter(1, "voxel", Spot);
        var vitals = new PlayerVitals();
        vitals.Damage(1000);
        var perks = new Perks();
        e.Begin(Calm, vitals, perks);
        Say(e, vitals, perks, "What is this place");
        Say(e, vitals, perks, "You mentioned");
        Say(e, vitals, perks, "Take it");
        Assert.True(vitals.Dead);
        Assert.Equal(0, perks.ParryWindowBonus);
    }

    [Fact]
    public void ChoicesAreRememberedAndChangeLaterTalk()
    {
        var progress = new StoryProgress();
        var e = new WulverEncounter(1, "voxel", Spot);
        var vitals = new PlayerVitals();
        var perks = new Perks();
        e.Begin(Calm, vitals, perks);
        Say(e, vitals, perks, "Who are you");
        Say(e, vitals, perks, "Tell me about the yew");
        Say(e, vitals, perks, "Tell me the old story");
        Assert.Contains(WulverDialogue.AskedWhoIAm, e.Flags);
        Assert.Contains(WulverDialogue.HeardYew, e.Flags);
        progress.SetWulver("voxel", e.Flags, e.Visits);

        var back = new WulverEncounter(1, "voxel", Spot, progress.WulverFlags("voxel"), progress.WulverVisits("voxel"));
        var r2 = back.Begin(Calm, new PlayerVitals(), new Perks());
        Assert.Equal("ret_welcome", r2.NodeId);
        Pick(r2, "Sit a while");
        Assert.Contains("who I was", r2.Text);
        Pick(r2, "The yew");
        Assert.StartsWith("Back to the yew", r2.Text);
    }

    // ---- greeting ----

    [Fact]
    public void HeGreetsOncePerApproachAndAgainOnlyAfterYouHaveLeftAndReturned()
    {
        var e = new WulverEncounter(1, "voxel", Spot);
        Assert.Null(e.Approach(10.5 + 30, 61, 10.5));
        Assert.Null(e.Approach(10.5 + 9, 61, 10.5));
        Assert.Equal(WulverDialogue.GreetingFirst, e.Approach(10.5 + 6.5, 61, 10.5));
        for (double d = 6; d >= 0.5; d -= 0.5) Assert.Null(e.Approach(10.5 + d, 61, 10.5));
        Assert.Null(e.Approach(10.5 + 8, 61, 10.5)); // stepping back a little is not leaving
        Assert.Null(e.Approach(10.5 + 6, 61, 10.5));
        Assert.Null(e.Approach(10.5 + 12, 61, 10.5));
        Assert.Null(e.Approach(10.5 + 9, 61, 10.5));
        Assert.NotNull(e.Approach(10.5 + 6, 61, 10.5));
    }

    [Fact]
    public void TheGreetingIsGenericAndTurnsIntoAReturnGreetingOnceMet()
    {
        var e = new WulverEncounter(1, "ewan", Spot);
        Assert.Equal(WulverDialogue.GreetingFirst, e.Approach(10.5, 61, 10.5 + 5));
        e.Begin(Calm, new PlayerVitals(), new Perks());
        e.Approach(10.5, 61, 10.5 + 15);
        Assert.Equal(WulverDialogue.GreetingReturn, e.Approach(10.5, 61, 10.5 + 5));
        Assert.DoesNotContain("Ewan", WulverDialogue.GreetingFirst + WulverDialogue.GreetingReturn);
    }

    [Fact]
    public void HeNoticesYouWithinSevenBlocksNotFromFarOrFarBelowAndTalksWithinThree()
    {
        var e = new WulverEncounter(1, "voxel", Spot);
        Assert.True(e.Notices(10.5 + 6.9, 61, 10.5));
        Assert.False(e.Notices(10.5 + 7.5, 61, 10.5));
        Assert.False(e.Notices(10.5, 20, 10.5));
        Assert.True(e.IsNear(10.5, 61, 12.5));
        Assert.False(e.IsNear(10.5, 61, 14.5));
        Assert.False(e.IsNear(10.5, 70, 10.5));
    }

    [Fact]
    public void ItsGreetingSoundRendersSoftly()
    {
        var samples = Synth.Render(SoundId.WulverGreeting);
        Assert.InRange(samples.Length / (double)Synth.SampleRate, 0.5, 3);
        Assert.InRange(samples.Max(Math.Abs), 0.05, 0.8);
        Assert.All(samples, s => Assert.True(float.IsFinite(s)));
    }

    [Fact]
    public void ItsGiftChimeIsASoftShortRisingPhrase()
    {
        var samples = Synth.Render(SoundId.WulverGift);
        double seconds = samples.Length / (double)Synth.SampleRate;
        Assert.InRange(seconds, 0.8, 2.5);
        Assert.InRange(samples.Max(Math.Abs), 0.1, 0.8);
        Assert.All(samples, s => Assert.True(float.IsFinite(s)));
        // It fades out to near silence by the end, and is the same every time it is made.
        Assert.True(samples.Skip(samples.Length - 200).Max(Math.Abs) < 0.02);
        Assert.Equal(samples, Synth.Render(SoundId.WulverGift));
        // New sounds are appended at the end of the list, so none of the earlier ids shift.
        Assert.Equal(SoundId.WulverGreeting + 1, SoundId.WulverGift);
    }

    // ---- exploring every path ----------------------------------------------------------------------

    private static readonly string[] Seeds = ["voxel", "ewan", "Èoghann", "Ewan-Croft", "ewanc26", "hollow"];

    private static IEnumerable<DialogueContext> Contexts()
    {
        var flagSets = new[] { WulverDialogue.ClaimedMaker, WulverDialogue.MakerKey, WulverDialogue.MakerKnown };
        var profiles = new (double Health, int Deaths, int Kills, int Deepest, int Notes, bool Key)[]
        {
            (1, 0, 0, -1, 0, false),
            (0.2, 5, 40, 2, 5, true),
            (1, 0, 5, 2, 0, true),
            (1, 5, 0, -1, 0, false),
            (1, 0, 0, 2, 0, false),
            (1, 0, 0, -1, 5, false),
        };
        foreach (var seed in Seeds)
            foreach (bool ending in new[] { false, true })
                foreach (int visits in new[] { 0, 1, 3 })
                    for (int mask = 0; mask < 8; mask++)
                        foreach (var p in profiles)
                            yield return new DialogueContext
                            {
                                Seed = seed.Length * 31,
                                SeedText = seed,
                                VisitCount = visits,
                                HealthFraction = p.Health,
                                Deaths = p.Deaths,
                                Kills = p.Kills,
                                DeepestFloor = p.Deepest,
                                FloorCount = 3,
                                NotesRead = p.Notes,
                                HasKey = p.Key,
                                EndingReached = ending,
                                Flags = new HashSet<string>(flagSets.Where((_, i) => (mask & (1 << i)) != 0)),
                            };
    }

    /// <summary>Every state reachable by any sequence of choices from each starting context (a node, with what is remembered).</summary>
    private static IEnumerable<(DialogueContext Start, DialogueRunner Runner)> Explore(IEnumerable<DialogueContext> starts)
    {
        foreach (var start in starts)
        {
            var seen = new HashSet<string>();
            var stack = new Stack<DialogueRunner>();
            stack.Push(new DialogueRunner(Graph, start));
            while (stack.Count > 0)
            {
                var r = stack.Pop();
                if (r.Finished) continue;
                string key = $"{r.NodeId}|{string.Join(",", r.Context.Flags.Order())}|{r.Context.VisitCount}|{r.Context.GiftGiven}";
                if (!seen.Add(key)) continue;
                yield return (start, r);
                for (int i = 0; i < r.Choices.Count; i++)
                {
                    var next = r.Fork();
                    next.Advance(i);
                    stack.Push(next);
                }
            }
        }
    }

    [Fact]
    public void EwanIsSpokenOnlyAfterTheClaimAndOnlyWithACircumstanceOnEveryPathUnderEveryContext()
    {
        int states = 0, withName = 0;
        foreach (var (_, r) in Explore(Contexts()))
        {
            states++;
            bool has = r.Text.Contains("Ewan", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Eoghann", r.Text);
            Assert.DoesNotContain(r.Choices, c => c.Text.Contains("Ewan", StringComparison.OrdinalIgnoreCase));
            if (!has) continue;
            withName++;
            Assert.True(r.Context.Has(WulverDialogue.ClaimedMaker), $"{r.NodeId} names him without the claim");
            Assert.True(WulverDialogue.MakerCircumstance.Evaluate(r.Context), $"{r.NodeId} names him without a circumstance (seed {r.Context.SeedText})");
        }
        Assert.True(states > 5000, $"explored only {states} states");
        Assert.True(withName > 100, "the maker path must be reachable in some contexts");
    }

    [Fact]
    public void OnAnOrdinarySeedAndRunNoPathEverShowsTheName()
    {
        var start = new DialogueContext { Seed = 1, SeedText = "voxel", FloorCount = 3 };
        var states = Explore([start, start with { VisitCount = 2, NotesRead = 9, Deaths = 5 }]).ToList();
        Assert.True(states.Count > 30);
        Assert.DoesNotContain(states, s => s.Runner.Text.Contains("Ewan"));
        // Even claiming to be the maker, on an ordinary seed, only earns the kind reply.
        var r = new DialogueRunner(Graph, start);
        Pick(r, "Who are you");
        Pick(r, "I built this hill");
        Assert.Equal("claim_plain", r.NodeId);
        Assert.Contains("A good many folk say that, and I let them", r.Text);
        Assert.DoesNotContain("Ewan", r.Text);
    }

    [Fact]
    public void OnlyNodesGuardedByTheClaimAndACircumstanceContainTheName()
    {
        foreach (var node in Graph.Nodes)
        {
            bool names = node.Lines.Any(l => (l.Text + string.Concat(l.Alternatives ?? [])).Contains("Ewan"));
            if (!names) continue;
            Assert.NotNull(node.Guard);
            // The guard must fail unless the claim is made AND a circumstance holds: try every combination of atoms.
            foreach (var ctx in Contexts())
                if (node.Guard!.Evaluate(ctx))
                    Assert.True(ctx.Has(WulverDialogue.ClaimedMaker) && WulverDialogue.MakerCircumstance.Evaluate(ctx), node.Id);
        }
    }

    [Theory]
    [InlineData("ewan")]
    [InlineData("Eòghann")]
    [InlineData("EWAN CROFT")]
    [InlineData("ewanc26")]
    public void CircumstanceAOnTheMakersOwnSeedTheClaimBringsTheName(string seed)
    {
        var r = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = seed, FloorCount = 3 });
        Pick(r, "Who are you");
        Pick(r, "I built this hill");
        Assert.Equal("mk_1", r.NodeId);
        Assert.StartsWith("Ewan.", r.Text);
        string all = "";
        for (int i = 0; i < 5; i++)
        {
            Pick(r, "Go on");
            all += r.Text;
        }
        Assert.Contains("every stair and stone", all);
        Assert.Contains("same name", all);
        Assert.Contains("ook up at the sky", all);
        Pick(r, "Thank him");
        Assert.Equal("mk_farewell", r.NodeId);
        Assert.Contains("Go well, Ewan", r.Text);
        Assert.Contains(WulverDialogue.MakerKnown, r.Context.Flags);
    }

    [Fact]
    public void CircumstanceBTheEndingAndThreeVisitsAreEnoughAndTwoAreNot()
    {
        DialogueRunner Claim(int visits, bool ending)
        {
            var r = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = visits, EndingReached = ending, FloorCount = 3 });
            Pick(r, "Sit a while");
            Pick(r, "I made this place");
            return r;
        }
        Assert.Equal("mk_1", Claim(3, true).NodeId);
        Assert.Equal("mk_1", Claim(2, true).NodeId); // this visit is the third
        Assert.Equal("claim_plain", Claim(1, true).NodeId);
        Assert.Equal("claim_plain", Claim(5, false).NodeId);
    }

    [Fact]
    public void CircumstanceCCarryingAKeyFromTheLastFloorToHimAfterTheClaimEarnsTheNameButNotByAccident()
    {
        // Claim on an ordinary seed: the kind reply, and the claim is remembered.
        var first = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 1, FloorCount = 3 });
        Pick(first, "Sit a while");
        Pick(first, "I made this place");
        Assert.Equal("claim_plain", first.NodeId);
        var flags = first.Context.Flags;
        Assert.Contains(WulverDialogue.ClaimedMaker, flags);

        // A key held, but not from the last floor: nothing.
        var shallow = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 2, FloorCount = 3, HasKey = true, DeepestFloor = 1, Flags = flags });
        Assert.Equal("ret_key", shallow.NodeId);
        // The last floor, but no key: nothing.
        Assert.Equal("ret_welcome", new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 2, FloorCount = 3, DeepestFloor = 2, Flags = flags }).NodeId.Replace("ret_deep", "ret_welcome"));
        // No claim first, key from the last floor: nothing.
        Assert.Equal("ret_key", new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 2, FloorCount = 3, HasKey = true, DeepestFloor = 2 }).NodeId);

        // Claim, key, last floor: he asks, and the key earns the name.
        var deep = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 2, FloorCount = 3, HasKey = true, DeepestFloor = 2, Flags = flags });
        Assert.Equal("key_recognise", deep.NodeId);
        Assert.DoesNotContain("Ewan", deep.Text);
        Pick(deep, "Show him the key");
        Assert.Equal("mk_1", deep.NodeId);
        Assert.Contains("Ewan", deep.Text);
        Assert.Contains(WulverDialogue.MakerKey, deep.Context.Flags);
    }

    [Fact]
    public void OnceKnownLaterVisitsGreetEwanByNameWithDifferentLines()
    {
        var seen = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "ewan", FloorCount = 3 });
        Pick(seen, "Who are you");
        Pick(seen, "I built this hill");
        var flags = seen.Context.Flags;
        Assert.Contains(WulverDialogue.MakerKnown, flags);

        var back = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "ewan", VisitCount = 1, FloorCount = 3, Flags = flags });
        Assert.Equal("mk_return", back.NodeId);
        Assert.StartsWith("Ewan.", back.Text);
        Assert.True(back.Context.VisitCount == 2);
        Pick(back, "How is the hill");
        Assert.Equal("mk_return_2", back.NodeId);
        Assert.NotEqual(seen.Text, back.Text);
    }

    [Fact]
    public void AClaimMadeEarlierBecomesTheNameOnceTheEndingAndThreeVisitsAreReached()
    {
        var back = new DialogueRunner(Graph, new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 3, EndingReached = true, FloorCount = 3, Flags = new HashSet<string> { WulverDialogue.ClaimedMaker } });
        Assert.Equal("mk_1", back.NodeId);
        Assert.Contains("Ewan", back.Text);
    }

    // ---- hints are true and gated ----

    [Fact]
    public void NewPlayersHearTheBasicsAndVeteransHearShorterLines()
    {
        string Hint(string choice, bool veteran)
        {
            var ctx = new DialogueContext { Seed = 1, SeedText = "voxel", VisitCount = 1, Kills = veteran ? 40 : 0, DeepestFloor = veteran ? 2 : 0, FloorCount = 3 };
            var r = new DialogueRunner(Graph, ctx);
            Pick(r, "Sit a while");
            Pick(r, "The long way down");
            Pick(r, choice);
            return r.Text;
        }
        foreach (var topic in new[] { "Locked chests", "Sever", "Parrying" })
        {
            string basic = Hint(topic, false), vet = Hint(topic, true);
            Assert.True(vet.Length < basic.Length, topic);
        }
        Assert.Contains("key", Hint("Locked chests", false));
        Assert.Contains("yellow", Hint("Parrying", false));
        Assert.Contains("Sever", Hint("Sever", false));
        Assert.Contains("Meteor", Hint("Sever", false));
    }

    [Fact]
    public void ALeaveOptionIsAlwaysOpen()
    {
        foreach (var (_, r) in Explore(Contexts().Take(300)))
            Assert.Contains(r.Choices, c => c.Next == null || Graph.Find(c.Next) is { } n && DialogueValidator.IsTerminal(n));
    }

    // ---- persistence ----

    [Fact]
    public void FlagsAndVisitsRoundTripThroughTheProgressFile()
    {
        var p = new StoryProgress();
        p.SetWulver("voxel", ["heardYew", "gaveFishBack"], 4);
        p.SetWulver("other seed", ["claimedMaker"], 1);
        var back = StoryProgress.FromJson(p.ToJson());
        Assert.Equal(["heardYew", "gaveFishBack"], back.WulverFlags("voxel"));
        Assert.Equal(4, back.WulverVisits("voxel"));
        Assert.Equal(["claimedMaker"], back.WulverFlags("other seed"));
        Assert.Empty(back.WulverFlags("never"));
        Assert.Equal(0, back.WulverVisits("never"));
    }

    [Fact]
    public void TheProgressFileIsBoundedAndLoadedDefensively()
    {
        var p = new StoryProgress();
        p.SetWulver("s", Enumerable.Range(0, 100).Select(i => $"flag{i}").Append("bad flag!").Append(new string('x', 40)), 99999999);
        Assert.Equal(WulverEncounter.MaxFlags, p.WulverFlags("s").Count);
        Assert.DoesNotContain("bad flag!", p.WulverFlags("s"));
        Assert.Equal(10_000, p.WulverVisits("s"));

        foreach (var junk in new[]
        {
            """{"seeds":{"a":{"wulver":5}}}""",
            """{"seeds":{"a":{"wulver":{"visits":"many","flags":[1,null,{"x":1},"ok_flag","white space","ok_flag"]}}}}""",
            """{"seeds":{"a":{"wulver":{"flags":"nope","visits":-4}}}}""",
            """{"seeds":{"a":{"wulver":{"visits":1e99}}}}""",
        })
        {
            var back = StoryProgress.FromJson(junk);
            Assert.All(back.WulverFlags("a"), f => Assert.True(WulverEncounter.ValidFlag(f)));
            Assert.InRange(back.WulverVisits("a"), 0, 10_000);
        }
        Assert.Equal(["ok_flag"], StoryProgress.FromJson("""{"seeds":{"a":{"wulver":{"flags":["ok_flag","white space","ok_flag"]}}}}""").WulverFlags("a"));
    }

    [Fact]
    public void TheEncounterSavesNothingInvalidAndKeepsAtMostThirtyTwoFlags()
    {
        var e = new WulverEncounter(1, "voxel", Spot, Enumerable.Range(0, 80).Select(i => $"f{i}").Append("no good"), -5);
        Assert.Equal(WulverEncounter.MaxFlags, e.Flags.Count);
        Assert.Equal(0, e.Visits);
    }

    [Fact]
    public void ARealSaveAndLoadKeepsTheFlags()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hollowfall-wulver-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
        try
        {
            var p = new StoryProgress();
            p.SetWulver("voxel", ["heardYew"], 2);
            Assert.True(p.TrySave(path));
            var back = StoryProgress.Load(path);
            Assert.Equal(["heardYew"], back.WulverFlags("voxel"));
            Assert.Equal(2, back.WulverVisits("voxel"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
