using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class AgentSystem : ComponentSystem
{
    private EntityManager em = Unity.Entities.World.Active.EntityManager;
    private AgentComp _agentComp;
    private bool assigned = false;
    
    
    
    protected override void OnUpdate()
    {
        Entities.ForEach((ref AgentComp agentComp, ref Translation translation) =>
        {
            if (!assigned)
            {
                assigned = true;
                _agentComp = agentComp;
            }
            agentComp.spent += Time.deltaTime;
            if (Vector3.Distance(translation.Value.xyz, agentComp.target) < Mathf.Epsilon)
                translation.Value = new float3(agentComp.target);
            else
                translation.Value = Vector3.Slerp(translation.Value, agentComp.target, agentComp.spent / agentComp.dur);
        });
    }

    public void ClearTrace()
    {
        _agentComp.Trace.Clear();
        _agentComp.PerceptedPlaces = new Dictionary<Position, Percepts>();
        _agentComp.KnowledgeOfPlaces = new Dictionary<Position, Knowledge>();
    }

    public void PerceiveCurrentPosition(Percepts percepts)
    {
        _agentComp.PerceptedPlaces[_agentComp.CurrentPosition] = percepts;
        _agentComp.KnowledgeOfPlaces[_agentComp.CurrentPosition] = new Knowledge();

        if (percepts.Glitter) 
            _agentComp.FoundGold = true;

        var newPlacesToGo = PossibleMoves().Where(pos => !_agentComp.PerceptedPlaces.ContainsKey(pos));

        foreach (var position in newPlacesToGo)
        {
            var hasKnowledge = _agentComp.KnowledgeOfPlaces.ContainsKey(position);

            if (hasKnowledge)
            {
                var knowledge = _agentComp.KnowledgeOfPlaces[position];
                if (!percepts.Stench && knowledge.MightHaveWumpus) knowledge.MightHaveWumpus = false;

                if (!percepts.Breeze && knowledge.MightHavePit) knowledge.MightHavePit = false;
            }
            else
            {
                _agentComp.KnowledgeOfPlaces[position] = new Knowledge
                {
                    MightHaveWumpus = percepts.Stench,
                    MightHavePit = percepts.Breeze
                };
            }
        }
    }
    
    
     private List<Position> PossibleMoves()
    {
        var possiblePositions = new List<Position>();

        if (_agentComp.CurrentPosition.X > 0) // we can go west
            possiblePositions.Add(new Position(_agentComp.CurrentPosition.X - 1, _agentComp.CurrentPosition.Y));

        if (_agentComp.CurrentPosition.X < _agentComp.WorldWidth - 1) // we can go east
            possiblePositions.Add(new Position(_agentComp.CurrentPosition.X + 1, _agentComp.CurrentPosition.Y));

        if (_agentComp.CurrentPosition.Y > 0) // we can go south
            possiblePositions.Add(new Position(_agentComp.CurrentPosition.X, _agentComp.CurrentPosition.Y - 1));

        if (_agentComp.CurrentPosition.Y < _agentComp.WorldHeight - 1) // we can go north
            possiblePositions.Add(new Position(_agentComp.CurrentPosition.X, _agentComp.CurrentPosition.Y + 1));

        return possiblePositions;
    }
     
    private bool IKnowItIsSafe(Position position)
    {
        return _agentComp.KnowledgeOfPlaces.ContainsKey(position) &&
               !_agentComp.KnowledgeOfPlaces[position].MightHaveWumpus &&
               !_agentComp.KnowledgeOfPlaces[position].MightHavePit;
    }
}