using System;
using System.Collections.Generic;

public class RelicManager
{
    public event Action<Relic> OnRelicAddedEvent;
    public event Action<Relic> OnRelicRemovedEvent;

    private List<Relic> relics = new List<Relic>();
    private Player owner;

    public RelicManager(Player owner)
    {
        this.owner = owner;
    }

    public void AddRelic(string relicId)
    {
        Relic relic = ModObjectFactory.CreateRelic(relicId, owner);
        if (relic != null)
        {
            relics.Add(relic);
            
            // Player's event bus triggers (using RelicData for backward compatibility)
            owner.eventBus.Invoke<IOnAddRelic>(c => c.OnAddRelic(relic.Data));
            
            OnRelicAddedEvent?.Invoke(relic);
            UnityEngine.Debug.Log($"Relic Added : {relic.Data.relicName}");
        }
    }

    public void RemoveRelic(string relicId)
    {
        Relic relic = relics.Find(r => r.Data != null && r.Data.relicName == relicId);
        if (relic != null)
        {
            relics.Remove(relic);
            owner.eventBus.Invoke<IOnRemoveRelic>(c => c.OnRemoveRelic(relic.Data));
            relic.Dispose();
            OnRelicRemovedEvent?.Invoke(relic);
            UnityEngine.Debug.Log($"Relic Removed : {relic.Data.relicName}");
        }
    }

    public Relic GetRandomRelic()
    {
        if (relics.Count == 0) return null;
        return relics[UnityEngine.Random.Range(0, relics.Count)];
    }

    public Relic GetSpecificRelic(string relicId)
    {
        return relics.Find(r => r.Data != null && r.Data.relicName == relicId);
    }
    
    public IReadOnlyList<Relic> GetRelics()
    {
        return relics;
    }

    public void ClearRelics()
    {
        foreach (var relic in relics)
        {
            if (relic != null)
            {
                relic.Dispose();
                OnRelicRemovedEvent?.Invoke(relic);
            }
        }
        relics.Clear();
    }
}
