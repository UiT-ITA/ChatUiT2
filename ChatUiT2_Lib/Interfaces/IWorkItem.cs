﻿using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel;

namespace ChatUiT2.Interfaces;

public interface IWorkItem
{
    public string Id { get; init; }
    public string Name { get; set; }
    public WorkItemType Type { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
    public bool IsFavorite { get; set; }
    public bool Persistant { get; set; }
    public WorkItemState State { get; set; }
}

public enum WorkItemType
{
    Chat
}
public enum WorkItemState
{
    Unloaded,
    Loading,
    Ready
}