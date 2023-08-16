using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class UIManager : SystemBase // this should not rely on any other systems! Other systems may use it though!
{
    VisualElement Root;

    protected override void OnStartRunning()
    {
        Entity UIEntity = EntityManager.CreateEntity();
        EntityManager.AddComponent<UIData>(UIEntity);

        ref UIData UIInfo = ref SystemAPI.GetSingletonRW<UIData>().ValueRW;

        Root = Object.FindObjectOfType<UIDocument>().rootVisualElement;
    }
    protected override void OnUpdate()
    {
        ref UIData UIInfo = ref SystemAPI.GetSingletonRW<UIData>().ValueRW;

        if (UIInfo.Change)
        {
            UIInfo.Change = false;

            DeloadMenu(UIInfo.PreviousState);

            LoadMenu(UIInfo.State);
        }
    }

    void LoadMenu(UIState State)
    {
        switch (State)
        {
            case UIState.MainMenu:
                LoadMainMenu();
                break;
        }
    }

    void DeloadMenu(UIState State)
    {
        switch (State)
        {
            case UIState.MainMenu:
                DeloadMainMenu();
                break;
        }
    }

    #region Menus

    #region TemplateMenu
    void LoadTemplateMenu()
    {
        Root.Q<VisualElement>("TemplateMenu").style.display = DisplayStyle.Flex;
    }

    void DeloadTemplateMenu()
    {
        Root.Q<VisualElement>("TemplateMenu").style.display = DisplayStyle.None;
    }
    #endregion

    #region MainMenu
    void LoadMainMenu()
    {
        Root.Q<VisualElement>("MainMenu").style.display = DisplayStyle.Flex;
    }

    void DeloadMainMenu()
    {
        Root.Q<VisualElement>("MainMenu").style.display = DisplayStyle.None;
    }
    #endregion

    #endregion
}

public struct UIData : IComponentData
{
    //public UIState State
    //{
    //    get => StoredState;

    //    set
    //    {
    //        if (StoredState != value)
    //        {
    //            StoredState = value;
    //            Change = true;
    //        }
    //    }
    //}

    public UIState State;
    public UIState PreviousState;
    public bool Change; // probably needs a rename later


}

public enum UIState // do I want this to be bitwise or not?
{
    Alive = 0,
    Dead = 1,
    PerksAndCurses = 2,
    MainMenu = 3,
    MainMenuGameOver = 4,
    Almanac = 5,
    AlmanacDead = 6,
    Settings = 7
}