using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TechCosmos.UniversalSelection.Runtime
{
    /// <summary>
    /// 通用选择管理器基类，提供完整的单位选择功能
    /// 支持框选、多选、叠加选择等操作
    /// 用户只需继承此类并指定单位类型T，实现GetAllUnits方法即可使用
    /// </summary>
    /// <typeparam name="T">要管理的单位类型</typeparam>
    public abstract class SelectionManager<T> : MonoBehaviour
    {
        /// <summary>
        /// 当前选中的单位列表
        /// </summary>
        public List<T> SelectedUnits { get; private set; } = new List<T>();

        private static SelectionManager<T> _instance;

        /// <summary>
        /// 选择管理器的单例实例
        /// </summary>
        public static SelectionManager<T> Instance { get { return _instance; } }

        // 框选相关
        private Vector2 _selectionStartPos;
        private bool _isSelecting = false;
        private Rect _selectionRect;
        protected T[] _allUnits;

        /// <summary>
        /// 是否正在框选操作中
        /// </summary>
        public bool IsSelecting => _isSelecting;

        /// <summary>
        /// 从单位实例获取Transform的委托
        /// 如果单位类型不是Component或GameObject，需要设置此委托
        /// </summary>
        public System.Func<T, Transform> GetTransformFromUnit { get; set; }

        /// <summary>
        /// 当单位被选中时触发的事件
        /// </summary>
        public event System.Action<T> OnUnitSelected;

        /// <summary>
        /// 当选择被清空时触发的事件
        /// </summary>
        public event System.Action OnSelectionCleared;

        /// <summary>
        /// 当需要显示选择框时触发的事件，传递选择框的矩形区域
        /// </summary>
        public event System.Action<Rect> OnSelectionBoxDisplay;

        /// <summary>
        /// 当需要获取所有单位时触发的事件
        /// </summary>
        public event System.Action OnGetAllUnits;

        /// <summary>
        /// 当需要设置单位选中效果时触发的事件，传递单位和选中状态
        /// </summary>
        public event System.Action<T, bool> OnSetUnitSlectedEffect;

        /// <summary>
        /// 选中指定单位
        /// </summary>
        /// <param name="unit">要选中的单位</param>
        public virtual void SelectUnit(T unit)
        {
            if (unit != null)
            {
                SelectedUnits.Add(unit);
                OnUnitSelected?.Invoke(unit);
            }
        }

        /// <summary>
        /// 添加单位到当前选择（多选模式）
        /// </summary>
        /// <param name="unit">要添加的单位</param>
        public virtual void AddToSelection(T unit)
        {
            if (unit != null && !SelectedUnits.Contains(unit))
            {
                SelectedUnits.Add(unit);
                OnSetUnitSlectedEffect?.Invoke(unit, true);
            }
        }

        /// <summary>
        /// 清空当前所有选择
        /// </summary>
        public virtual void ClearSelection()
        {
            foreach (var unit in SelectedUnits)
            {
                OnSetUnitSlectedEffect(unit, false);
            }
            SelectedUnits.Clear();
            OnSelectionCleared?.Invoke();
        }

        /// <summary>
        /// 开始框选操作
        /// </summary>
        /// <param name="startPos">框选起始位置（屏幕坐标）</param>
        public virtual void StartSelection(Vector2 startPos)
        {
            _selectionStartPos = startPos;
            _isSelecting = true;
            _selectionRect = new Rect();
        }

        /// <summary>
        /// 更新框选操作
        /// </summary>
        /// <param name="currentPos">当前鼠标位置（屏幕坐标）</param>
        public virtual void UpdateSelection(Vector2 currentPos)
        {
            if (!_isSelecting) return;

            // 计算选择框
            _selectionRect.xMin = Mathf.Min(_selectionStartPos.x, currentPos.x);
            _selectionRect.xMax = Mathf.Max(_selectionStartPos.x, currentPos.x);
            _selectionRect.yMin = Mathf.Min(_selectionStartPos.y, currentPos.y);
            _selectionRect.yMax = Mathf.Max(_selectionStartPos.y, currentPos.y);
            UpdateAreaSelectionEffect();
            // 通知UI显示选择框
            OnSelectionBoxDisplay?.Invoke(_selectionRect);
        }

        /// <summary>
        /// 更新区域选择效果，显示框选范围内的单位预览效果
        /// </summary>
        public virtual void UpdateAreaSelectionEffect()
        {
            T[] allUnits = GetAllUnits();
            foreach (T unit in allUnits)
            {
                Transform unitTransform = GetUnitTransform(unit);

                Vector2 unitScreenPos = Camera.main.WorldToScreenPoint(unitTransform.position);
                if (_selectionRect.Contains(unitScreenPos))
                {
                    OnSetUnitSlectedEffect?.Invoke(unit, true);
                }
            }
        }

        /// <summary>
        /// 结束框选操作并确定最终选择
        /// </summary>
        /// <param name="endPos">框选结束位置（屏幕坐标）</param>
        public virtual void FinishSelection(Vector2 endPos)
        {
            if (!_isSelecting) return;

            UpdateSelection(endPos);
            T[] allUnits = GetAllUnits();
            foreach (T unit in allUnits)
            {
                OnSetUnitSlectedEffect?.Invoke(unit, false);
            }

            _isSelecting = false;
            // 隐藏选择框
            OnSelectionBoxDisplay?.Invoke(new Rect(0, 0, 0, 0));
        }

        /// <summary>
        /// 在指定矩形区域内选择单位
        /// </summary>
        /// <param name="start">区域起始点（屏幕坐标）</param>
        /// <param name="end">区域结束点（屏幕坐标）</param>
        public virtual void SelectUnitsInArea(Vector2 start, Vector2 end)
        {
            // 计算选择区域
            Rect selectionArea = new Rect();
            selectionArea.xMin = Mathf.Min(start.x, end.x);
            selectionArea.xMax = Mathf.Max(start.x, end.x);
            selectionArea.yMin = Mathf.Min(start.y, end.y);
            selectionArea.yMax = Mathf.Max(start.y, end.y);

            T[] allUnits = GetAllUnits();
            List<T> unitsInArea = new List<T>();
            foreach (T unit in allUnits)
            {
                Vector2 unitScreenPos = Camera.main.WorldToScreenPoint(GetUnitTransform(unit).position);

                if (selectionArea.Contains(unitScreenPos))
                {
                    unitsInArea.Add(unit);
                }
            }
            // 选择区域内的单位
            if (unitsInArea.Count > 0)
            {
                // 如果按住Shift键，添加到当前选择
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    foreach (T unit in unitsInArea)
                    {
                        if (!SelectedUnits.Contains(unit))
                        {
                            AddToSelection(unit);
                        }
                    }
                }
                else
                {
                    ClearSelection();
                    foreach (T unit in unitsInArea)
                    {
                        AddToSelection(unit);
                    }
                }

                Debug.Log($"Selected {unitsInArea.Count} units in area");
            }
            else
            {
                // 如果点击空白区域且没有按住Shift，清空选择
                if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    ClearSelection();
                }
            }
        }

        /// <summary>
        /// 获取场景中所有可选择的单位
        /// 此方法需要由子类实现，需要用户提供具体的单位获取逻辑
        /// </summary>
        /// <returns>所有可选择的单位数组</returns>
        public abstract T[] GetAllUnits();

        /// <summary>
        /// 从单位实例获取其Transform组件
        /// 如果单位类型不是Component或GameObject，需要设置GetTransformFromUnit委托或重写此方法
        /// </summary>
        /// <param name="unit">单位实例</param>
        /// <returns>单位的Transform组件，如果无法获取则返回null</returns>
        public virtual Transform GetUnitTransform(T unit)
        {
            if (GetTransformFromUnit != null)
            {
                return GetTransformFromUnit(unit);
            }

            // 默认实现：如果T是Component或GameObject
            if (unit is Component component)
            {
                return component.transform;
            }
            else if (unit is GameObject gameObject)
            {
                return gameObject.transform;
            }

            Debug.LogWarning($"No transform getter specified for type {typeof(T)}");
            return null;
        }
    }
}
