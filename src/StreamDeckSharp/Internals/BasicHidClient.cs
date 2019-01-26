﻿using OpenMacroBoard.SDK;
using System;

namespace StreamDeckSharp.Internals
{
    internal class BasicHidClient : IStreamDeckBoard
    {
        private readonly byte[] keyStates;
        private readonly object disposeLock = new object();
        protected readonly OutputReportGenerator reportGenerator;
        protected readonly IHardwareInternalInfos hardwareInformation;

        private bool disposed;
        protected IStreamDeckHid deckHid;

        public BasicHidClient(IStreamDeckHid deckHid, IHardwareInternalInfos hardwareInformation)
        {
            this.deckHid = deckHid;
            Keys = hardwareInformation.Keys;

            deckHid.ConnectionStateChanged += (s, e) => ConnectionStateChanged?.Invoke(this, e);

            this.hardwareInformation = hardwareInformation;
            keyStates = new byte[Keys.Count];

            reportGenerator = new OutputReportGenerator(
                deckHid.OutputReportLength,
                hardwareInformation.ReportSize,
                hardwareInformation.StartReportNumber
            );
        }

        public GridKeyPositionCollection Keys { get; }
        IKeyPositionCollection IMacroBoard.Keys => Keys;

        public bool IsConnected => deckHid.IsConnected;
        public event EventHandler<KeyEventArgs> KeyStateChanged;
        public event EventHandler<ConnectionEventArgs> ConnectionStateChanged;

        public void Dispose()
        {
            lock (disposeLock)
            {
                if (disposed)
                    return;
                disposed = true;
            }

            Shutdown();

            ShowLogoWithoutDisposeVerification();
            deckHid.Dispose();

            Dispose(true);
        }

        protected virtual void Shutdown() { }
        protected virtual void Dispose(bool managed) { }

        public void SetBrightness(byte percent)
        {
            VerifyNotDisposed();
            deckHid.WriteFeature(GetBrightnessMsg(percent));
        }

        public virtual void SetKeyBitmap(int keyId, KeyBitmap bitmapData)
        {
            keyId = hardwareInformation.ExtKeyIdToHardwareKeyId(keyId);

            var payload = hardwareInformation.GeneratePayload(bitmapData);
            reportGenerator.Initialize(payload, keyId);

            while (reportGenerator.HasNextReport)
                deckHid.WriteReport(reportGenerator.GetNextReport());
        }

        public void ShowLogo()
        {
            VerifyNotDisposed();
            ShowLogoWithoutDisposeVerification();
        }

        protected void VerifyNotDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(BasicHidClient));
        }

        public void ProcessKeys()
        {
            var newStates = deckHid.ReadReport();
            for (int i = 0; i < keyStates.Length; i++)
            {
                if (keyStates[i] != newStates[i])
                {
                    var externalKeyId = hardwareInformation.HardwareKeyIdToExtKeyId(i);
                    KeyStateChanged?.Invoke(this, new KeyEventArgs(externalKeyId, newStates[i] != 0));
                    keyStates[i] = newStates[i];
                }
            }
        }

        private void ShowLogoWithoutDisposeVerification()
        {
            deckHid.WriteFeature(showLogoMsg);
        }

        private static byte[] GetBrightnessMsg(byte percent)
        {
            if (percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
            var buffer = new byte[] { 0x05, 0x55, 0xaa, 0xd1, 0x01, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            buffer[5] = percent;
            return buffer;
        }

        private static readonly byte[] showLogoMsg = new byte[] { 0x0B, 0x63 };
    }
}
