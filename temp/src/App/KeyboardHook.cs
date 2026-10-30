using System;
using System.Runtime.InteropServices;

namespace KeyboardGuitar
{
    // WH_KEYBOARD_LL 전역 훅. 설치한 스레드(UI 스레드)의 메시지 루프에서 콜백이 호출된다.
    public sealed class KeyboardHook : IDisposable
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104;
        const uint LLKHF_EXTENDED = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vk);

        readonly InputRouter router;
        readonly HookProc proc;   // GC가 수거하지 않도록 보관
        IntPtr handle;

        public KeyboardHook(InputRouter router)
        {
            this.router = router;
            router.IsPhysicallyDown = vk => (GetAsyncKeyState(vk) & 0x8000) != 0;
            proc = Callback;
            handle = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("키보드 훅 설치 실패: " + Marshal.GetLastWin32Error());
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                int vk = (int)k.vkCode;
                // vk 0xFF, 확장 플래그가 붙은 L Shift(E0 2A)는 키보드가 보내는 가짜 이벤트
                bool fake = vk == 0xFF || (k.scanCode == KeyCodes.LShift && (k.flags & LLKHF_EXTENDED) != 0);
                if (!fake)
                {
                    int id = k.scanCode != 0
                        ? KeyCodes.FromScan((int)k.scanCode, (k.flags & LLKHF_EXTENDED) != 0)
                        : 0x10000 | vk;
                    bool swallow;
                    try { swallow = router.Process(id, vk, isDown); }
                    catch { swallow = false; }
                    if (swallow) return (IntPtr)1;
                }
            }
            return CallNextHookEx(handle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(handle);
                handle = IntPtr.Zero;
            }
        }
    }
}
