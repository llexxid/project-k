namespace KingdomIdle.UI
{
    // 루트 화면(교체)
    public enum UIScreenId
    {
        Title = 0,
        Main = 1,
        Dungeon = 2,
    }

    // 기능 패널(스택) - BottomTab/햄버거 메뉴로 여는 큰 창들
    public enum UIPanelId
    {
        KingdomArmy = 0,
        Growth = 1,
        Gacha = 2,
        Shop = 3,

        HamburgerMenu = 10,
        Mailbox = 11,
        Achievements = 12,
        Notice = 13,
        EventList = 14,
        Settings = 15,
    }

    // 상세/확인 팝업(스택)
    public enum UIPopupId
    {
        Confirm = 0,
        ItemDetail = 1,
        SkillDetail = 2,
        JobDetail = 3,
        Reward = 4,
        Error = 5,
        OfflineReward = 6,
    }

    // 시스템 오버레이(스택과 분리)
    public enum UIOverlayId
    {
        Loading = 0,
        Toast = 1,
        TutorialBlocker = 2,
    }
}
