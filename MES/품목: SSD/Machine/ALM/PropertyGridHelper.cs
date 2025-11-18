using System.Reflection;

/// <summary>
/// PropertyGrid 컨트롤 작업을 위한 유틸리티 메서드 제공
/// 리플렉션을 사용하여 PropertyGrid의 내부 기능에 접근
/// </summary>
public static class PropertyGridHelper
{
    #region Public Methods
    /// <summary>
    /// PropertyGrid에서 속성 이름으로 해당 항목 선택
    /// </summary>
    /// <param name="propertyGrid">대상 PropertyGrid 컨트롤</param>
    /// <param name="propertyName">선택할 속성 이름</param>
    /// <returns>항목 선택 성공 여부</returns>
    public static bool SelectPropertyGridItemByName(PropertyGrid propertyGrid, string propertyName)
    {
        var gridItem = FindGridItemByName(propertyGrid, propertyName);
        if (gridItem == null) return false;

        // 해당 항목 선택 및 포커스 설정
        propertyGrid.SelectedGridItem = gridItem;
        gridItem.Select();
        SendKeys.Send("{TAB}"); // 편집 모드로 전환
        return true;
    }

    /// <summary>
    /// PropertyGrid의 내용을 새로 고침 (UI 스레드 안전)
    /// </summary>
    /// <param name="grid">대상 PropertyGrid</param>
    /// <param name="selectedObject">표시할 객체</param>
    public static void RefreshPropertyGrid(PropertyGrid grid, object selectedObject)
    {
        if (grid.InvokeRequired)
        {
            // UI 스레드에서 실행되도록 Invoke 사용
            grid.Invoke(new Action<PropertyGrid, object>(RefreshPropertyGrid), grid, selectedObject);
            return;
        }

        grid.SelectedObject = selectedObject;
        grid.Refresh(); // 화면 갱신
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// PropertyGrid 내에서 속성 이름으로 GridItem 찾기
    /// </summary>
    private static GridItem FindGridItemByName(PropertyGrid propertyGrid, string propertyName)
    {
        // 리플렉션을 사용하여 PropertyGrid의 내부 메서드 접근
        var getPropEntriesMethod = propertyGrid.GetType().GetMethod("GetPropEntries", 
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (getPropEntriesMethod == null) 
        {
            Logger.Warn("GetPropEntries method not found in PropertyGrid");
            return null;
        }

        // PropertyGrid의 모든 항목 컬렉션 가져오기
        var gridItemCollection = (GridItemCollection)getPropEntriesMethod.Invoke(propertyGrid, null);
        return TraverseGridItems(gridItemCollection, propertyName);
    }

    /// <summary>
    /// GridItem 컬렉션을 재귀적으로 탐색하여 원하는 항목 찾기
    /// </summary>
    private static GridItem TraverseGridItems(IEnumerable parentGridItemCollection, string propertyName)
    {
        foreach (GridItem gridItem in parentGridItemCollection)
        {
            // 레이블이 일치하는 항목 찾기 (대소문자 무시)
            if (gridItem.Label != null && 
                gridItem.Label.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return gridItem;

            // 자식 항목이 없으면 다음 항목으로 이동
            if (gridItem.GridItems == null) continue;

            // 자식 항목 재귀적 탐색
            var childGridItem = TraverseGridItems(gridItem.GridItems, propertyName);
            if (childGridItem != null) return childGridItem;
        }

        return null; // 항목을 찾지 못함
    }
    #endregion
}
