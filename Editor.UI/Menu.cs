﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MapsExt.UI
{
	public enum MenuPosition
	{
		DOWN,
		RIGHT
	}

	public enum MenuTrigger
	{
		CLICK,
		HOVER
	}

	public class Menu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
	{
		public GameObject contentTemplate;
		public Graphic graphic;
		public Button itemButton;
		public Text itemLabel;
		public Text itemHotkeyLabel;
		public Text label;
		public Color normal;
		public Color highlighted;
		public Color active;
		public MenuPosition position;
		public MenuTrigger openTrigger;
		public MenuState state = MenuState.INACTIVE;
		public List<MenuItem> items = new List<MenuItem>();
		public Action onOpen;
		public Action onClose;
		public Action onHighlight;

		public enum MenuState
		{
			INACTIVE,
			HIGHLIGHTED,
			ACTIVE,
			DISABLED
		}

		private Dictionary<string, MenuItem> itemsByKey = new Dictionary<string, MenuItem>();
		private GameObject content;

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (this.state == MenuState.DISABLED || this.openTrigger == MenuTrigger.HOVER)
			{
				return;
			}

			if (this.state == MenuState.INACTIVE)
			{
				this.SetState(MenuState.HIGHLIGHTED);
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (this.state == MenuState.DISABLED || this.openTrigger == MenuTrigger.HOVER)
			{
				return;
			}

			if (this.state == MenuState.HIGHLIGHTED)
			{
				this.SetState(MenuState.INACTIVE);
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (this.state == MenuState.DISABLED || this.openTrigger == MenuTrigger.HOVER)
			{
				return;
			}

			var rectTransform = this.gameObject.GetComponent<RectTransform>();
			bool pressedButton = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position);

			if (this.state == MenuState.ACTIVE && pressedButton)
			{
				this.SetState(MenuState.HIGHLIGHTED);
			}
			else
			{
				this.SetState(MenuState.ACTIVE);
			}
		}

		public void SetState(MenuState state)
		{
			bool wasOpened = this.state != MenuState.ACTIVE && state == MenuState.ACTIVE;
			bool wasClosed = this.state == MenuState.ACTIVE && state != MenuState.ACTIVE;
			bool wasHighlighted = this.state == MenuState.INACTIVE && state == MenuState.HIGHLIGHTED;

			this.state = state;
			Color newGraphicColor = this.normal;
			Color newLabelColor = Color.white;

			if (state == MenuState.INACTIVE)
			{
				newGraphicColor = this.normal;
				this.content.SetActive(false);
			}

			if (state == MenuState.HIGHLIGHTED)
			{
				newGraphicColor = this.highlighted;
				this.content.SetActive(false);
			}

			if (state == MenuState.ACTIVE)
			{
				newGraphicColor = this.active;
				this.content.SetActive(true);
			}

			if (state == MenuState.DISABLED)
			{
				newLabelColor.a = 0.3f;
				this.content.SetActive(false);
			}

			if (this.graphic != null)
			{
				this.graphic.color = newGraphicColor;
			}

			if (this.label != null)
			{
				this.label.color = newLabelColor;
			}

			if (wasOpened)
			{
				this.onOpen?.Invoke();
			}

			if (wasClosed)
			{
				this.onClose?.Invoke();
			}

			if (wasHighlighted)
			{
				this.onHighlight?.Invoke();
			}
		}

		public void Start()
		{
			this.content = GameObject.Instantiate(this.contentTemplate, this.transform);

			this.SetState(MenuState.INACTIVE);

			var rectTransform = this.content.GetComponent<RectTransform>();

			if (this.position == MenuPosition.DOWN)
			{
				rectTransform.anchorMin = new Vector2(0, 0);
				rectTransform.anchorMax = new Vector2(0, 0);
				rectTransform.pivot = new Vector2(0, 1);
			}
			else if (this.position == MenuPosition.RIGHT)
			{
				rectTransform.anchorMin = new Vector2(1, 1);
				rectTransform.anchorMax = new Vector2(1, 1);
				rectTransform.pivot = new Vector2(0, 1);
			}

			this.contentTemplate.SetActive(false);
			this.itemButton.gameObject.SetActive(false);

			this.RedrawContent();
		}

		public void Update()
		{
			if (this.state == MenuState.DISABLED)
			{
				return;
			}

			bool isHovered = this.IsMouseInsideMenu();

			if (this.openTrigger == MenuTrigger.CLICK && Input.GetMouseButtonDown(0) && !isHovered)
			{
				this.SetState(MenuState.INACTIVE);
			}

			if (this.openTrigger == MenuTrigger.HOVER)
			{
				this.SetState(isHovered ? MenuState.ACTIVE : MenuState.INACTIVE);
			}

			var sortedItems = this.items
				.Where(item => item.keyBinding != null && item.keyBinding.key != null && item.keyBinding.key.code != KeyCode.None)
				.ToList();

			sortedItems.Sort((a, b) =>
			{
				return b.keyBinding.modifiers.Length - a.keyBinding.modifiers.Length;
			});

			var calledItem = sortedItems.Find(item =>
			{
				return Input.GetKeyDown(item.keyBinding.key.code) && item.keyBinding.modifiers.All(m => Input.GetKey(m.code));
			});

			if (calledItem != null && !calledItem.disabled)
			{
				calledItem.action?.Invoke();
			}
		}

		private bool IsMouseInsideMenu()
		{
			var rectTransforms = this.gameObject.GetComponentsInChildren<RectTransform>().ToList();
			rectTransforms.Add(this.gameObject.GetComponent<RectTransform>());
			return rectTransforms.Any(rt => RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition));
		}

		public void AddItem(MenuItem item)
		{
			this.PatchMenuItemActions(item);
			this.RegisterItem(item);
			this.items.Add(item);
			this.RedrawContent();
		}

		public void SetItemEnabled(string key, bool enabled)
		{
			if (this.itemsByKey.TryGetValue(key, out MenuItem item))
			{
				if (item.disabled == enabled)
				{
					item.disabled = !enabled;
					this.RedrawContent();
				}
			}
		}

		private void PatchMenuItemActions(MenuItem item)
		{
			var oldAction = item.action;
			if (oldAction != null)
			{
				item.action = () =>
				{
					oldAction();
					this.SetState(MenuState.INACTIVE);
				};
			}

			var subitems = item.items ?? new List<MenuItem>() { };
			foreach (var subitem in subitems)
			{
				this.PatchMenuItemActions(subitem);
			}
		}

		private void RegisterItem(MenuItem item, string key = "")
		{
			key += item.label;
			this.itemsByKey.Add(key, item);

			if (item.items != null)
			{
				key += ".";
				foreach (var subitem in item.items)
				{
					this.RegisterItem(subitem, key);
				}
			}
		}

		public void RedrawContent()
		{
			if (this.content == null)
			{
				return;
			}

			foreach (Transform child in this.content.transform)
			{
				GameObject.Destroy(child.gameObject);
			}

			foreach (var item in this.items)
			{
				this.itemLabel.text = item.label;

				if (item.keyBinding != null)
				{
					var keys = item.keyBinding.modifiers.ToList();
					keys.Add(item.keyBinding.key);
					keys = keys.Where(k => k != null).ToList();
					this.itemHotkeyLabel.text = string.Join("+", keys.Select(k => k.name));
				}
				else if (item.items != null)
				{
					this.itemHotkeyLabel.text = "<color=#aaaaaa>></color>";
				}
				else
				{
					this.itemHotkeyLabel.text = "";
				}

				var icol = this.itemLabel.color;
				var hcol = this.itemHotkeyLabel.color;
				this.itemLabel.color = item.disabled ? new Color(icol.r, icol.g, icol.b, 0.5f) : new Color(icol.r, icol.g, icol.b, 1);
				this.itemHotkeyLabel.color = item.disabled ? new Color(hcol.r, hcol.g, hcol.b, 0.5f) : new Color(hcol.r, hcol.g, hcol.b, 1);
				this.itemButton.interactable = !item.disabled;

				var instance = GameObject.Instantiate(this.itemButton.gameObject, this.content.transform);
				instance.SetActive(true);

				if (item.disabled)
				{
					continue;
				}

				var button = instance.GetComponent<Button>();
				button.onClick.AddListener(() => item.action?.Invoke());

				if (item.items != null)
				{
					instance.SetActive(false);

					var submenu = instance.AddComponent<Menu>();
					submenu.contentTemplate = this.contentTemplate;
					submenu.graphic = null;
					submenu.itemButton = this.itemButton;
					submenu.itemLabel = this.itemLabel;
					submenu.itemHotkeyLabel = this.itemHotkeyLabel;
					submenu.label = this.label;
					submenu.normal = this.normal;
					submenu.highlighted = this.highlighted;
					submenu.active = this.active;
					submenu.position = MenuPosition.RIGHT;
					submenu.openTrigger = MenuTrigger.HOVER;
					submenu.items = item.items.ToList();

					instance.SetActive(true);
				}
			}
		}
	}

	[Serializable]
	public class MenuItem
	{
		public string label;
		public Action action;
		public KeyCombination keyBinding;
		public List<MenuItem> items;
		public bool disabled;

		public MenuItem()
		{
			this.label = null;
			this.action = null;
			this.keyBinding = null;
			this.items = null;
			this.disabled = false;
		}
	}

	public class MenuItemBuilder
	{
		private readonly MenuItem item;

		public MenuItemBuilder()
		{
			this.item = new MenuItem();
		}

		public MenuItemBuilder Label(string label)
		{
			this.item.label = label;
			return this;
		}

		public MenuItemBuilder Action(Action action)
		{
			this.item.action = action;
			return this;
		}

		public MenuItemBuilder KeyBinding(NamedKeyCode hotkey, params NamedKeyCode[] modifiers)
		{
			this.item.keyBinding = new KeyCombination
			{
				key = hotkey,
				modifiers = modifiers
			};
			return this;
		}

		public MenuItem Item()
		{
			return this.item;
		}

		public MenuItemBuilder SubItem(Action<MenuItemBuilder> cb)
		{
			var subBuilder = new MenuItemBuilder();
			cb(subBuilder);

			if (this.item.items == null)
			{
				this.item.items = new List<MenuItem>();
			}

			this.item.items.Add(subBuilder.Item());
			return this;
		}
	}

	[Serializable]
	public class KeyCombination
	{
		public NamedKeyCode[] modifiers;
		public NamedKeyCode key;
	}

	[Serializable]
	public class NamedKeyCode
	{
		public KeyCode code;
		public string name;

		public NamedKeyCode(KeyCode code, string name)
		{
			this.code = code;
			this.name = name;
		}
	}
}
