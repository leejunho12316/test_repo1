using System;
using System.Collections.Generic;

namespace KeyboardGuitar
{
    // 전역 키 입력을 받아 차단 여부를 정하고 동작을 알린다. 훅 콜백에서 바로 호출되므로 빨라야 한다.
    public class InputRouter
    {
        const int VK_TAB = 0x09, VK_RETURN = 0x0D, VK_CONTROL = 0x11, VK_MENU = 0x12;
        const int VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_DOWN = 0x28, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;

        public bool Active;
        public bool Capturing;  // 키 매칭 창에서 칸을 고른 상태

        public event Action ToggleRequested;
        public event Action ResetRequested;
        public event Action BoardRequested;    // ← 누름: 지판 보기 시작
        public event Action BoardReleased;     // ← 뗌: 지판 숨기기
        public event Action<int> NoteKey;
        public event Action<int> CaptureKey;

        // 키 → 마지막 이벤트 시각. 뗌을 놓친 키(Ctrl+Alt+Del, 잠금 화면 등)는 오래되면 버린다.
        readonly Dictionary<int, int> down = new Dictionary<int, int>();
        readonly HashSet<int> swallowed = new HashSet<int>();
        readonly HashSet<int> ctrlDown = new HashSet<int>();
        readonly HashSet<int> altDown = new HashSet<int>();
        const int StaleMs = 1500;   // 눌린 키는 자동 반복으로 계속 이벤트가 오므로 이보다 오래 조용하면 뗀 것으로 본다

        public Func<int> Clock = () => Environment.TickCount;
        // vk가 실제로 눌려 있는지 (앱에서 GetAsyncKeyState로 연결). 다른 프로그램에 보낸 키만 OS 상태가 정확하다.
        public Func<int, bool> IsPhysicallyDown;
        readonly Dictionary<int, int> vkOf = new Dictionary<int, int>();
        int boardKey = -1;   // 지판 보기로 누르고 있는 ← 키
        int lastEvent;       // 마지막 키 이벤트 시각
        public const int BoardStaleMs = 10000;   // ← 뗌을 놓쳤다고 보는 시간 (키 이벤트가 하나도 없을 때)

        // true = 다른 프로그램에 보내지 않음
        public bool Process(int id, int vk, bool isDown)
        {
            bool isCtrl = vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;
            bool isAlt = vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU;
            lastEvent = Clock();

            if (!isDown)
            {
                Forget(id);
                if (id == boardKey) ReleaseBoard();
                // 누를 때 막은 키만 뗄 때도 막는다. (켜기 전에 누른 키가 눌린 채로 남지 않게)
                return swallowed.Remove(id);
            }

            int now = Clock();
            PruneStale(now, id);
            bool repeat = down.ContainsKey(id);
            down[id] = now;
            vkOf[id] = vk;
            if (isCtrl) ctrlDown.Add(id);
            if (isAlt) altDown.Add(id);

            // 자동 반복은 처음 누를 때의 결정을 따른다. (누름·뗌 짝이 어긋나지 않게)
            if (repeat) return swallowed.Contains(id);

            bool swallow = Decide(id, vk, isCtrl, isAlt);
            if (swallow) swallowed.Add(id);
            else swallowed.Remove(id);
            return swallow;
        }

        void Forget(int id)
        {
            down.Remove(id);
            ctrlDown.Remove(id);
            altDown.Remove(id);
        }

        void PruneStale(int now, int except)
        {
            var stale = new List<int>();
            foreach (var kv in down)
            {
                // ←는 다른 키를 치는 동안 반복 신호가 멈춰도 계속 누르고 있을 수 있으므로 시간으로 정리하지 않는다. (CheckBoard 참고)
                if (kv.Key == except || kv.Key == boardKey || unchecked(now - kv.Value) <= StaleMs) continue;
                // 다른 키를 함께 누르면 먼저 누른 키는 반복 신호가 멈춘다. OS에 보낸 키는 실제 상태로 확인한다.
                int vk;
                if (!swallowed.Contains(kv.Key) && IsPhysicallyDown != null && vkOf.TryGetValue(kv.Key, out vk) && IsPhysicallyDown(vk)) continue;
                stale.Add(kv.Key);
            }
            foreach (int id in stale) Forget(id);
        }

        bool Decide(int id, int vk, bool isCtrl, bool isAlt)
        {
            bool ctrl = ctrlDown.Count > 0, alt = altDown.Count > 0;
            bool isWin = vk == VK_LWIN || vk == VK_RWIN;

            if (vk == VK_RETURN && ctrl && !alt)
            {
                Raise(ToggleRequested);
                return true;
            }

            if (Capturing)
            {
                if (isAlt || isWin || (alt && vk == VK_TAB)) return false;
                // 단일 키만: 이 키 말고 눌린 키가 있으면 조합으로 보고 무시
                if (down.Count == 1 && !KeyCodes.IsUnassignable(id) && CaptureKey != null) CaptureKey(id);
                return true;
            }

            if (!Active) return false;
            if (isWin || isAlt || (alt && vk == VK_TAB)) return false;
            // → 초기화, ← 지판 보기, ↑ ↓는 막기만 한다.
            if (vk >= VK_LEFT && vk <= VK_DOWN)
            {
                if (vk == VK_RIGHT) Raise(ResetRequested);
                else if (vk == VK_LEFT)
                {
                    boardKey = id;
                    Raise(BoardRequested);
                }
                return true;
            }
            if (!isCtrl && NoteKey != null) NoteKey(id);
            return true;
        }

        // 주기적으로 불린다. ←를 누른 채 키 이벤트가 오래 없으면(잠금 화면 등으로 뗌을 놓친 경우) 지판을 내린다.
        // ←만 누르고 있으면 자동 반복 신호가 계속 오므로 여기에 걸리지 않는다.
        public void CheckBoard()
        {
            if (boardKey < 0 || unchecked(Clock() - lastEvent) <= BoardStaleMs) return;
            Forget(boardKey);
            ReleaseBoard();
        }

        // 잠금·세션 전환 등 키 뗌을 놓칠 수 있는 상황에서 지판을 바로 내린다.
        public void CancelBoard()
        {
            if (boardKey < 0) return;
            Forget(boardKey);
            ReleaseBoard();
        }

        void ReleaseBoard()
        {
            boardKey = -1;
            Raise(BoardReleased);
        }

        static void Raise(Action a)
        {
            if (a != null) a();
        }
    }
}
