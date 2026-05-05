using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace grid_image_viewer
{
    public class SlideshowService : ISlideshowService, IDisposable
    {
        private readonly IViewerStateService _state;
        private readonly SettingsManager _settings;
        private readonly DispatcherTimer _timer;
        private readonly Random _random = new Random();
        private readonly List<int> _remainingIndices = new List<int>();
        private int _lastPlaylistCount = 0;

        public bool IsRunning => _state.IsSlideshowRunning;
        public int[] CurrentRandomIndices { get; private set; } = new int[4] { -1, -1, -1, -1 };

        public SlideshowService(IViewerStateService state, SettingsManager settings)
        {
            _state = state;
            _settings = settings;
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            if (IsRunning) return;

            _timer.Interval = TimeSpan.FromSeconds(_settings.SlideshowInterval);
            _timer.Start();
            _state.IsSlideshowRunning = true;

            InitializeRandomIndices();

            WeakReferenceMessenger.Default.Send(new SlideshowStateChangedMessage(true));
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _timer.Stop();
            _state.IsSlideshowRunning = false;
            for (int i = 0; i < 4; i++) CurrentRandomIndices[i] = -1;

            WeakReferenceMessenger.Default.Send(new SlideshowStateChangedMessage(false));
        }

        public void UpdateInterval(double seconds)
        {
            _timer.Interval = TimeSpan.FromSeconds(seconds);
        }

        private void InitializeRandomIndices()
        {
            _remainingIndices.Clear();
            _lastPlaylistCount = _state.Playlist.Count;
            if (_lastPlaylistCount > 0)
            {
                _remainingIndices.AddRange(Enumerable.Range(0, _lastPlaylistCount));
                _remainingIndices.Remove(_state.CurrentIndex);
                Shuffle();
            }
        }

        private void Timer_Tick(object? sender, object e)
        {
            if (_state.Playlist.Count == 0) return;

            if (_settings.SlideshowRandom)
            {
                HandleRandomNext();
            }
            else
            {
                HandleSequentialNext();
            }
        }

        private void HandleRandomNext()
        {
            var playlist = _state.Playlist;
            if (playlist.Count > _lastPlaylistCount)
            {
                for (int i = _lastPlaylistCount; i < playlist.Count; i++) _remainingIndices.Add(i);
                _lastPlaylistCount = playlist.Count;
                Shuffle();
            }

            if (_remainingIndices.Count == 0)
            {
                if (_settings.SlideshowLoop || _lastPlaylistCount <= 1)
                {
                    _remainingIndices.AddRange(Enumerable.Range(0, playlist.Count));
                    _remainingIndices.Remove(_state.CurrentIndex);
                    Shuffle();
                }
                else
                {
                    Stop();
                    return;
                }
            }

            int nextIdx = _remainingIndices[0];
            _remainingIndices.RemoveAt(0);

            int splits = _settings.MangaSplitCount;
            CurrentRandomIndices[0] = nextIdx;

            if (splits > 1 && _remainingIndices.Count >= splits - 1)
            {
                for (int i = 1; i < splits; i++)
                {
                    CurrentRandomIndices[i] = _remainingIndices[0];
                    _remainingIndices.RemoveAt(0);
                }
            }
            else
            {
                for (int i = 1; i < 4; i++) CurrentRandomIndices[i] = -1;
            }

            _state.CurrentIndex = nextIdx;
            WeakReferenceMessenger.Default.Send(new SlideshowNextRequestedMessage());
        }

        private void HandleSequentialNext()
        {
            int increment = _settings.MangaSplitCount;
            if (_state.CurrentIndex + increment >= _state.Playlist.Count)
            {
                if (_settings.SlideshowNextFolder)
                {
                    // For folder navigation, we still need the coordinator to handle it
                    WeakReferenceMessenger.Default.Send(new SlideshowNextRequestedMessage());
                }
                else if (_settings.SlideshowLoop)
                {
                    _state.CurrentIndex = 0;
                    WeakReferenceMessenger.Default.Send(new SlideshowNextRequestedMessage());
                }
                else
                {
                    Stop();
                }
            }
            else
            {
                _state.CurrentIndex += increment;
                WeakReferenceMessenger.Default.Send(new SlideshowNextRequestedMessage());
            }
        }

        private void Shuffle()
        {
            int n = _remainingIndices.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                int value = _remainingIndices[k];
                _remainingIndices[k] = _remainingIndices[n];
                _remainingIndices[n] = value;
            }
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
