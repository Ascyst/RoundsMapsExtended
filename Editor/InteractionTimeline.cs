﻿using MapsExt.Editor.MapObjects;
using MapsExt.MapObjects;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapsExt
{
	// An InteractionTimeline forms a one-dimensional timeline of MapObjectInteractions that can be traversed by "undoing" and "redoing".
	public class InteractionTimeline
	{
		private readonly List<MapObjectInteraction> interactionStack = new List<MapObjectInteraction>();
		private readonly Dictionary<int, MapObjectInstance[]> interactionIndexMapObjectInstances = new Dictionary<int, MapObjectInstance[]>();

		private int interactionIndex = -1;
		private MapObjectInstance[] interactionMapObjectInstances;
		private MapObjectManager mapObjectManager;

		public InteractionTimeline(MapObjectManager mapObjectManager)
		{
			this.mapObjectManager = mapObjectManager;
		}

		public void BeginInteraction(GameObject obj, bool isCreateInteraction = false)
		{
			this.BeginInteraction(obj.GetComponent<MapObjectInstance>(), isCreateInteraction);
		}

		public void BeginInteraction(MapObjectInstance instance, bool isCreateInteraction = false)
		{
			this.BeginInteraction(new MapObjectInstance[] { instance }, isCreateInteraction);
		}

		public void BeginInteraction(IEnumerable<GameObject> objects, bool isCreateInteraction = false)
		{
			this.BeginInteraction(objects.Select(obj => obj.GetComponent<MapObjectInstance>()), isCreateInteraction);
		}

		public void BeginInteraction(IEnumerable<MapObjectInstance> instances, bool isCreateInteraction = false)
		{
			MapObjectInteraction.BeginInteraction(mapObjectManager, instances);
			this.interactionMapObjectInstances = isCreateInteraction ? instances.ToArray() : null;
		}

		public void EndInteraction()
		{
			if (this.interactionIndex < this.interactionStack.Count - 1)
			{
				int removeFrom = this.interactionIndex + 1;
				int removeCount = this.interactionStack.Count - removeFrom;
				this.interactionStack.RemoveRange(removeFrom, removeCount);

				/* Deleting map objects really only disables them so that they can be easily retrieved during undo/redo. However, undoing and
				 * then interacting with map objects will branch into a logically new timeline, and the old branch is lost forever. If map objects
				 * were created in this old/deleted branch, they can never be retrieved, and so we can safely destroy those game objects.
				 */
				for (int i = removeFrom; i < removeFrom + removeCount; i++)
				{
					if (this.interactionIndexMapObjectInstances.TryGetValue(i, out MapObjectInstance[] list))
					{
						foreach (var obj in list)
						{
							GameObject.Destroy(obj.gameObject);
						}

						this.interactionIndexMapObjectInstances.Remove(i);
					}
				}
			}

			var interaction = MapObjectInteraction.EndInteraction();
			this.interactionStack.Add(interaction);
			this.interactionIndex++;

			if (this.interactionMapObjectInstances != null)
			{
				this.interactionIndexMapObjectInstances.Add(this.interactionIndex, this.interactionMapObjectInstances);
				this.interactionMapObjectInstances = null;
			}
		}

		public bool CanUndo()
		{
			return this.interactionIndex >= 0;
		}

		public bool CanRedo()
		{
			return this.interactionIndex < this.interactionStack.Count - 1;
		}

		public bool Undo()
		{
			if (!this.CanUndo())
			{
				return false;
			}

			var interaction = this.interactionStack[this.interactionIndex];
			interaction.Undo();
			this.interactionIndex--;
			return true;
		}

		public bool Redo()
		{
			if (!this.CanRedo())
			{
				return false;
			}

			this.interactionIndex++;
			var interaction = this.interactionStack[this.interactionIndex];
			interaction.Redo();
			return true;
		}
	}
}
