using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct AgentComp : IComponentData
{
    public Vector3 target;
    public float spent, dur;

    public int WorldHeight;
    public int WorldWidth;
    public Dictionary<Position, Percepts> PerceptedPlaces;
    public Dictionary<Position, Knowledge> KnowledgeOfPlaces;

    public bool FoundGold; //false

    public Position CurrentPosition; //(0,0)
    public Stack<Position> Trace; // = new Stack<Position>();
}