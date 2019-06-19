using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

[Serializable]
public struct Position : IEquatable<Position>
{
    public int X;
    public int Y;

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (X * 397) ^ Y;
        }
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (obj is Position)
        {
            var pos = (Position) obj;
            return Equals(obj);
        }

        return false;
    }

    public bool Equals(Position other)
    {
        return X == other.X && Y == other.Y;
    }
}

public struct Percepts
{
    public bool Stench;
    public bool Breeze;
    public bool Glitter;
}

public class Knowledge
{
    public bool MightHavePit;
    public bool MightHaveWumpus;
}


public class WumpusEntityManager : MonoBehaviour
{
    private const int gridsize = 4;
    public Material agentMat, doorMat, tileMat, ghostMat, pitMat, treasureMat;
    public Mesh blockMesh;

    private EntityManager EM;
    private AgentSystem _system;
    private Entity agent, Treasure;

    
    [SerializeField]
    private List<Position> WumpusPositions;
    [SerializeField]
    private List<Position> PitPositions;
    [SerializeField]
    private Position GoldPosition;

    private bool _gameRunning = true;
    public SoundEffect[] SoundEffects;
    private Dictionary<string, AudioClip> WumpusSounds;
    private AudioSource MoveAudioSrc;
    public AudioSource EffectsAudioSrc;
    public int numberOfIterations = 25;
    private int iterations = 0;
    private static string mode;
    private static string comment;
    private CaveWorld world;
    public float YPosition = 1.0f;
    
    
    [Serializable]
    public struct SoundEffect
    {
        public string Name;
        public AudioClip Sound;
    } 

    public void SetAgentSystem(AgentSystem agentSystem)
    {
        _system = agentSystem;
    }

    private void Awake()
    {
        EM = World.Active.EntityManager;

//        CreateWorldPlatform(EM);

//        MoveAgent(new Vector3(0,4,0));
        
        
        WumpusSounds = new Dictionary<string, AudioClip>();
        foreach (var pfb in SoundEffects)
        {
            WumpusSounds.Add(pfb.Name, pfb.Sound);
        }
        MoveAudioSrc = GetComponent<AudioSource>();
    }

    void Start () {
        mode = Application.isEditor ? "Editor" : "Release";
//        OpenLogFile($"Wumpus Unity ({mode}).csv");
        UnityEngine.Debug.Log("Iteration No.;Iterate time (microseconds);Comment");

        world = new CaveWorld(WumpusPositions, PitPositions, GoldPosition);
        CreateWorldPlatform();
//        _agent = Instantiate(WumpusPrefabs["Agent"], new Vector3(0, YPosition, 0), Quaternion.Euler(0, 180f, 0)).GetComponent<Agent>(); //Is instantiated in another place

        world.OnBreezePercepted += () =>
        {
            PlaySound("Breeze");
        };
        world.OnMove += (Position p) =>
        {
            
            var vecPos = new Vector3(p.X, YPosition, p.Y);
//            _agent.SetLerpPos(vecPos);
            MoveAgent(vecPos, 1);
            PlaySound("Move", false);
        };
        world.OnPitEncountered += () =>
        {
//            Destroy(_agent);
            UnityEngine.Debug.LogError("Dead");
            PlaySound("Pit");
            _gameRunning = false;
        };
        world.OnStenchPercepted += () => PlaySound("Stench");
        world.OnTreasureEncountered += () =>
        {

            EM.DestroyEntity(Treasure);
            
            PlaySound("Gold");
            comment = "gold";
        };
        world.OnWumpusEncountered += () =>
        {
//            Destroy(_agent);
// TODO Kill agent?
            PlaySound("Wumpus");
            _gameRunning = false;
        };
        world.OnGoalComplete += () =>
        {
            PlaySound("Goal");

            world.Reset();
            
            Treasure = EM.CreateEntity(typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));
                EM.SetSharedComponentData(Treasure, new RenderMesh
                {
                    mesh = blockMesh,
                    material = treasureMat
                });
                EM.SetComponentData(Treasure, new Translation
                {
                    Value = new float3(1, 1, 2)
                });

//            Treasure = Instantiate(WumpusPrefabs["Treasure"], new Vector3(GoldPosition.X, YPosition, GoldPosition.Y), Quaternion.Euler(0, 180f, 0));
            EM.SetComponentData(Treasure, new Translation
            {
                Value = new float3(1,YPosition, 2) //Hopefully the treasure position
            });
            iterations++;
            _gameRunning = iterations < numberOfIterations;
            comment = "reset";
        };
    }
    
    private void PlaySound(string soundName, bool specialEffect = true)
    {
        var audioSrc = specialEffect ? EffectsAudioSrc : MoveAudioSrc;

        audioSrc.clip = WumpusSounds[soundName];
        audioSrc.Play();
    }
    
    void MoveAgent(Vector3 newAgentPos, float moveDuration = 1f)
    {
//        _system._agentComp.SetLerpPos(newAgentPos, moveDuration);

        var ac = EM.GetComponentData<AgentComp>(agent);
        ac.CurrentPosition = new Position((int)newAgentPos.x, (int)newAgentPos.y);
        ac.target = newAgentPos;
        ac.spent = 0;
        EM.SetComponentData(agent, ac);
            
    }
    
    private float UpdateTimer = 0f;
    public float UpdateTimeSecs = 0.5f;
    private int iterationNumber;

    private void Update()
    {
        if (!_gameRunning)
        {
            return;
        }
        if (UpdateTimer > UpdateTimeSecs)
        {
            var t = new Stopwatch();
            t.Start();
            world.Iterate();
            UpdateTimer = 0f;
            t.Stop();
            UnityEngine.Debug.Log($"{iterationNumber};{t.Elapsed.TotalMilliseconds * 1000};{comment}");
            comment = "";
            iterationNumber++;
        }
        else
            UpdateTimer += Time.deltaTime;
    }
    
    private void CreateWorldPlatform()
    {
        //Agent archetype
        var agentArchetype = EM.CreateArchetype(
            typeof(AgentComp),
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));

        agent = EM.CreateEntity(agentArchetype);
        EM.SetComponentData(agent, new AgentComp
        {
            dur = 1,
            spent = 0,
            target = new Vector3(0, 1, 0)
        });

        EM.SetComponentData(agent, new Translation
        {
            Value = new float3(0, 1, 0)
        });

        EM.SetSharedComponentData(agent, new RenderMesh
        {
            mesh = blockMesh,
            material = agentMat
        });


        //Pit archetype
        var pitArchetype = EM.CreateArchetype(
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));
        
        //Ghost archetype
        var ghostArchetype = EM.CreateArchetype(
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));

        //Treasure archetype
        var treasureArchetype = EM.CreateArchetype(
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));
        
        //Border tile archetype
        var borderArchetype = EM.CreateArchetype(
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));

        //Border tile door
        var door = EM.CreateEntity(
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));

        EM.SetSharedComponentData(door, new RenderMesh
        {
            mesh = blockMesh,
            material = doorMat
        });

        EM.SetComponentData(door, new Translation
        {
            Value = new float3(0, 0, 0)
        });


        var entArray = new NativeArray<Entity>(gridsize * gridsize, Allocator.Temp);
        EM.CreateEntity(borderArchetype, entArray);

        var index = 0;

        for (var i = 0; i < gridsize; i++)
        for (var j = 0; j < gridsize; j++)
        {
            if (i == j && j == 0) //Door
                continue;


            var e = entArray[index];

            if (j == 2 && i == 0) //Ghost
            {
                var ghost = EM.CreateEntity(ghostArchetype);
                EM.SetSharedComponentData(ghost, new RenderMesh
                {
                    mesh = blockMesh,
                    material = ghostMat
                });

                EM.SetComponentData(ghost, new Translation
                {
                    Value = new float3(i, 1, j)
                });
            }
            else if (j != 1 && i == 2)
            {
                var pit = EM.CreateEntity(pitArchetype);
                EM.SetSharedComponentData(pit, new RenderMesh
                {
                    mesh = blockMesh,
                    material = pitMat
                });

                EM.SetComponentData(pit, new Translation
                {
                    Value = new float3(i, 1, j)
                });
            }
            else if (j == 2 && i == 1) //Treasure
            {
                Treasure = EM.CreateEntity(ghostArchetype);
                EM.SetSharedComponentData(Treasure, new RenderMesh
                {
                    mesh = blockMesh,
                    material = treasureMat
                });
                EM.SetComponentData(Treasure, new Translation
                {
                    Value = new float3(i, 1, j)
                });

            }

            EM.SetSharedComponentData(e, new RenderMesh
            {
                mesh = blockMesh,
                material = tileMat
            });

            EM.SetComponentData(e, new Translation
            {
                Value = new float3(i, 0, j)
            });

            index++;
        }


        entArray.Dispose();
    }
    
    class CaveWorld
    {
        public int WorldHeight = 4;
        public int WorldWidth = 4;


        private List<Position> Wumpi;
        private List<Position> Pits;
        public Position Gold;

        public AgentCat MrCat = new AgentCat();

        public event Action<Position> OnMove;
        public event Action OnWumpusEncountered;
        public event Action OnPitEncountered;
        public event Action OnTreasureEncountered;
        public event Action OnBreezePercepted;
        public event Action OnStenchPercepted;
        public event Action OnGoalComplete;

        public CaveWorld(List<Position> wumpi, List<Position> pits, Position gold)
        {
            Wumpi = wumpi;
            Pits = pits;
            Gold = gold;

            MrCat.TellMeAboutTheWorld(WorldWidth, WorldHeight);
        }

        public void Iterate()
        {
            var agentMove = MrCat.WhereIWannaGo();
            MrCat.CurrentPosition = agentMove;
            OnMove?.Invoke(agentMove);

            if (MrCat.FoundGold && MrCat.CurrentPosition.Equals(new Position(0, 0)))
                OnGoalComplete?.Invoke();

            if (WumpusAt(MrCat.CurrentPosition))
                OnWumpusEncountered?.Invoke();
            else if (PitAt(MrCat.CurrentPosition))
                OnPitEncountered?.Invoke();

            var percepts = GeneratePercepts();
            if (percepts.Breeze)
                OnBreezePercepted?.Invoke();
            if (percepts.Stench)
                OnStenchPercepted?.Invoke();
            if (percepts.Glitter)
                OnTreasureEncountered?.Invoke();

            MrCat.PerceiveCurrentPosition(percepts);
        }

        public void Reset()
        {
            MrCat.FoundGold = false;
            MrCat.ClearTrace();
        }

        Percepts GeneratePercepts()
        {
            var neighbours = GetNeighbours();

            return new Percepts
            {
                Breeze = neighbours.Any(PitAt),
                Stench = neighbours.Any(WumpusAt),
                Glitter = MrCat.CurrentPosition.Equals(Gold) && ! MrCat.FoundGold
            };
        }

        List<Position> GetNeighbours()
        {
            var possiblePositions = new List<Position>();

            if (MrCat.CurrentPosition.X > 0) // we can go west
            {
                possiblePositions.Add(new Position(MrCat.CurrentPosition.X - 1, MrCat.CurrentPosition.Y));
            }

            if (MrCat.CurrentPosition.X < WorldWidth - 1) // we can go east
            {
                possiblePositions.Add(new Position(MrCat.CurrentPosition.X + 1, MrCat.CurrentPosition.Y));
            }

            if (MrCat.CurrentPosition.Y > 0) // we can go south
            {
                possiblePositions.Add(new Position(MrCat.CurrentPosition.X, MrCat.CurrentPosition.Y - 1));
            }

            if (MrCat.CurrentPosition.Y < WorldHeight - 1) // we can go north
            {
                possiblePositions.Add(new Position(MrCat.CurrentPosition.X, MrCat.CurrentPosition.Y + 1));
            }

            return possiblePositions;
        }

        // Toby funcs
        public bool PitAt(Position position) => Pits.Any(pit => pit.Equals(position));
        public bool WumpusAt(Position position) => Wumpi.Any(wumpus => wumpus.Equals(position));
        public bool GoldAt(Position position) => Gold.Equals(position);
    }

    


    private class AgentCat
    {
        public Position CurrentPosition = new Position(0, 0);

        public bool FoundGold;
        private Dictionary<Position, Knowledge> KnowledgeOfPlaces = new Dictionary<Position, Knowledge>();


        private Dictionary<Position, Percepts> PerceptedPlaces = new Dictionary<Position, Percepts>();
        private readonly Stack<Position> Trace = new Stack<Position>();
        private int WorldHeight;
        private int WorldWidth;

        public void TellMeAboutTheWorld(int width, int height)
        {
            WorldWidth = width;
            WorldHeight = height;
        }

        public void ClearTrace()
        {
            Trace.Clear();
            PerceptedPlaces = new Dictionary<Position, Percepts>();
            KnowledgeOfPlaces = new Dictionary<Position, Knowledge>();
        }

        public void PerceiveCurrentPosition(Percepts percepts)
        {
            PerceptedPlaces[CurrentPosition] = percepts;
            KnowledgeOfPlaces[CurrentPosition] = new Knowledge();

            if (percepts.Glitter) FoundGold = true;

            var newPlacesToGo = PossibleMoves().Where(pos => !PerceptedPlaces.ContainsKey(pos));

            foreach (var position in newPlacesToGo)
            {
                var hasKnowledge = KnowledgeOfPlaces.ContainsKey(position);

                if (hasKnowledge)
                {
                    var knowledge = KnowledgeOfPlaces[position];
                    if (!percepts.Stench && knowledge.MightHaveWumpus) knowledge.MightHaveWumpus = false;

                    if (!percepts.Breeze && knowledge.MightHavePit) knowledge.MightHavePit = false;
                }
                else
                {
                    KnowledgeOfPlaces[position] = new Knowledge
                    {
                        MightHaveWumpus = percepts.Stench,
                        MightHavePit = percepts.Breeze
                    };
                }
            }
        }

        public Position WhereIWannaGo()
        {
            if (FoundGold)
            {
                if (Trace.Count == 0) return new Position(0, 0);
                return Trace.Pop();
            }
            // Find gold 'n kill wumpi

            var placesToGo = PossibleMoves();

            var placesIveBeen = placesToGo.Where(pos => PerceptedPlaces.ContainsKey(pos));
            var newPlacesToGo = placesToGo.Where(pos => !PerceptedPlaces.ContainsKey(pos));

            var safeNewPlacesToGo = newPlacesToGo.Where(IKnowItIsSafe);

            if (safeNewPlacesToGo.Any())
            {
                var move = safeNewPlacesToGo.First();
                Trace.Push(move);
                return move;
            }


            var safePlacesToGo = placesToGo.Where(IKnowItIsSafe);

            if (safePlacesToGo.Any())
            {
                var move = safePlacesToGo.First();
                Trace.Push(move);
                return move;
            }

            Console.WriteLine("I might die now :'(");
            var dangerousMove = newPlacesToGo.Any() ? newPlacesToGo.First() : placesToGo.First();
            Trace.Push(dangerousMove);
            return dangerousMove;
        }

        private bool IKnowItIsSafe(Position position)
        {
            return KnowledgeOfPlaces.ContainsKey(position) &&
                   !KnowledgeOfPlaces[position].MightHaveWumpus &&
                   !KnowledgeOfPlaces[position].MightHavePit;
        }


        private List<Position> PossibleMoves()
        {
            var possiblePositions = new List<Position>();

            if (CurrentPosition.X > 0) // we can go west
                possiblePositions.Add(new Position(CurrentPosition.X - 1, CurrentPosition.Y));

            if (CurrentPosition.X < WorldWidth - 1) // we can go east
                possiblePositions.Add(new Position(CurrentPosition.X + 1, CurrentPosition.Y));

            if (CurrentPosition.Y > 0) // we can go south
                possiblePositions.Add(new Position(CurrentPosition.X, CurrentPosition.Y - 1));

            if (CurrentPosition.Y < WorldHeight - 1) // we can go north
                possiblePositions.Add(new Position(CurrentPosition.X, CurrentPosition.Y + 1));

            return possiblePositions;
        }
    }
}