using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
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

    //Block archetype
    //Pit archetype
    //


    private void Start()
    {
        var EM = World.Active.EntityManager;

        //Agent archetype
        var agentArchetype = EM.CreateArchetype(
            typeof(AgentComp),
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));

        var ent = EM.CreateEntity(agentArchetype);
        EM.SetComponentData(ent, new AgentComp
        {
            dur = 1,
            spent = 0,
            target = new Vector3(1, 1, 5)
        });

        EM.SetComponentData(ent, new Translation
        {
            Value = new float3(0, 1, 0)
        });

        EM.SetSharedComponentData(ent, new RenderMesh
        {
            mesh = blockMesh,
            material = agentMat
        });


        //Ghost archetype
        var pitArchetype = EM.CreateArchetype(
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh));
        //Ghost archetype

        var ghostArchetype = EM.CreateArchetype(
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

    // Update is called once per frame
    private void Update()
    {
    }


    private class AgentCat
    {
        public readonly Position CurrentPosition = new Position(0, 0);

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