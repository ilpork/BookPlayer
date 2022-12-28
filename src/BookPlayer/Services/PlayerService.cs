﻿using BookPlayer.Interfaces;
using BookPlayer.Models;
using MediaManager;
using MediaManager.Library;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Xamarin.Forms;

namespace BookPlayer.Services
{
    public class PlayerService : IPlayerService, INotifyPropertyChanged
    {
        private readonly IFileHandlingService _fileHandlingService;
        private readonly IBookService _bookService;
        private Book _currentBook;
        private bool _isPlaying;
        private readonly Timer _timer;
        private string _currentTitle;
        private string _currentSubtitle;
        private TimeSpan _elapsed;
        private TimeSpan _duration;
        private double _currentProgress;
        private double _totalProgress;
        private string _bookCoverPath;
        private bool _isBookOpen;
        public event PropertyChangedEventHandler PropertyChanged;

        public PlayerService(IFileHandlingService fileHandlingService, IBookService bookService)
        {
            _fileHandlingService = fileHandlingService;
            _bookService = bookService;
            _bookService.SelectedBookChanged += BookService_SelectedBookChanged;

            _timer = new Timer
            {
                Interval = 30000
            };
            _timer.Elapsed += Timer_Elapsed;

            CrossMediaManager.Current.StepSizeBackward = new TimeSpan(0, 0, 10);
            CrossMediaManager.Current.StepSizeForward= new TimeSpan(0, 0, 10);
            CrossMediaManager.Current.MediaPlayer.AfterPlaying += MediaPlayer_AfterPlaying;
            CrossMediaManager.Current.PositionChanged += Current_PositionChanged;
            CrossMediaManager.Current.StateChanged += Current_StateChanged;
        }

        private async void BookService_SelectedBookChanged(object sender, Book e)
        {
            await CrossMediaManager.Current.Stop();

            OpenBook(e);
        }

        private void Current_StateChanged(object sender, MediaManager.Playback.StateChangedEventArgs e)
        {
            IsPlaying = e.State == MediaManager.Player.MediaPlayerState.Playing;        
            
            if (e.State == MediaManager.Player.MediaPlayerState.Paused)
            {
                UpdateProgress();
                _timer.Stop();
            }
            else
            {
                if (!_timer.Enabled)
                {
                    _timer.Start();
                }
            }
        }

        private void Current_PositionChanged(object sender, MediaManager.Playback.PositionChangedEventArgs e)
        {
            Elapsed = e.Position;
            CurrentProgress = Elapsed.TotalSeconds / Duration.TotalSeconds;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {            
            UpdateProgress();
        }

        public async Task SeekTo(double targetProgress)
        {            
            var newPosition = TimeSpan.FromSeconds(Duration.TotalSeconds * targetProgress);
            Elapsed = newPosition;
            await CrossMediaManager.Current.SeekTo(newPosition);            
        }

        public void OpenBook(Book book)
        {
            if (book == null)
            {
                return;
            }

            _currentBook = book;
            _currentBook.Files = Directory.GetFiles(_currentBook.Path, "*.mp3").OrderBy(file => file).ToList();
            BookCoverPath = book.CoverPath;

            CrossMediaManager.Current.Queue.MediaItems.Clear();
            var metadata = _fileHandlingService.GetBookMetadata(Path.Combine(_currentBook.Path, _currentBook.CurrentFile ?? _currentBook.Files.First()));
            UpdateMetadata(metadata);
            IsBookOpen = true;
        }

        public async Task PlayOrPause()
        {
            if (CrossMediaManager.Current.IsStopped())
            {
                if (CrossMediaManager.Current.Queue.Count == 0)
                {
                    foreach (var file in _currentBook.Files)
                    {
                        CrossMediaManager.Current.Queue.Add(new MediaItem(file));
                    }
                }

                if (_currentBook.CurrentFile != null)
                {
                    var currentItem = CrossMediaManager.Current.Queue.MediaItems.FirstOrDefault(item => item.MediaUri.EndsWith(_currentBook.CurrentFile));
                    var currentItemIndex = CrossMediaManager.Current.Queue.MediaItems.IndexOf(currentItem);
                    var currentProgress = _currentBook.CurrentProgress;

                    // Sometimes player jumps to the start of the first file if delays are not used (noticed on Android)
                    await CrossMediaManager.Current.Play();
                    await Task.Delay(40);
                    await CrossMediaManager.Current.PlayQueueItem(currentItemIndex);
                    await Task.Delay(40);
                    await CrossMediaManager.Current.MediaPlayer.SeekTo(currentProgress - new TimeSpan(0, 0, 10));
                }
                else
                {
                    await CrossMediaManager.Current.Play();
                }
            }
            else
            {                
                await CrossMediaManager.Current.PlayPause();
            }
        }

        private void MediaPlayer_AfterPlaying(object sender, MediaManager.Player.MediaPlayerEventArgs e)
        {
            if (CrossMediaManager.Current.IsPlaying())
            {
                var metadata = _fileHandlingService.GetBookMetadata(CrossMediaManager.Current.Queue.Current.MediaUri);
                UpdateMetadata(metadata);
            }
        }

        public async Task PlayPreviousFile()
        {
            await CrossMediaManager.Current.PlayPrevious();
        }

        public async Task PlayNextFile()
        {
            await CrossMediaManager.Current.PlayNext();
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (value != _isPlaying)
                {
                    _isPlaying = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying)));
                }

            }
        }

        public string CurrentTitle
        {
            get => _currentTitle;
            set
            {
                if (_currentTitle == null || !_currentTitle.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _currentTitle = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTitle)));
                }
            }
        }

        public string CurrentSubtitle
        {
            get => _currentSubtitle;
            set
            {
                if (_currentSubtitle == null || !_currentSubtitle.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _currentSubtitle = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSubtitle)));
                }
            }
        }

        public TimeSpan Elapsed
        {
            get => _elapsed;
            set
            {
                if (_elapsed == null || _elapsed.TotalMilliseconds != value.TotalMilliseconds)
                {
                    _elapsed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Elapsed)));
                }
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration == null || _duration.TotalMilliseconds != value.TotalMilliseconds)
                {
                    _duration = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Duration)));
                }
            }
        }

        public double CurrentProgress
        {
            get => _currentProgress;
            set
            {
                if (_currentProgress != value)
                {
                    _currentProgress = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProgress)));
                }
            }
        }

        public double TotalProgress
        {
            get => _totalProgress;
            set
            {
                if (_totalProgress != value)
                {
                    _totalProgress = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalProgress)));
                }
            }
        }

        public string BookCoverPath
        {
            get => _bookCoverPath;
            set
            {
                if (_bookCoverPath != value)
                {
                    _bookCoverPath = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BookCoverPath)));
                }
            }
        }

        public bool IsBookOpen
        {
            get => _isBookOpen;
            set
            {
                if (_isBookOpen != value)
                {
                    _isBookOpen = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBookOpen)));
                }
            }
        }

        private void UpdateMetadata(BookMetadata metaData)
        {
            if (metaData != null)
            {
                CurrentTitle = metaData.Title;
                CurrentSubtitle = metaData.SubTitle;
                
                if (CrossMediaManager.Current.Queue.Current != null)
                {
                    CrossMediaManager.Current.Queue.Current.DisplayTitle = metaData.Title;
                    CrossMediaManager.Current.Queue.Current.DisplaySubtitle = metaData.SubTitle;
                    DependencyService.Get<IMediaService>().AddMetaData(CrossMediaManager.Current.Queue.Current, metaData.SubTitle);
                }
                
                Duration = metaData.Duration;
                CurrentProgress = _currentBook.CurrentProgress.TotalSeconds / metaData.Duration.TotalSeconds;
                _currentBook.TotalElapsedTime = metaData.TotalElapsedTime + _currentBook.CurrentProgress;
                TotalProgress = (double)_currentBook.TotalElapsedTime.Seconds / _currentBook.TotalTime.TotalSeconds;
                Elapsed = _currentBook.CurrentProgress;
            }
        }

        private void UpdateProgress()
        {            
            if (CrossMediaManager.Current.Queue.Current != null)
            {
                _currentBook.CurrentProgress = CrossMediaManager.Current.Position;
                _currentBook.CurrentFile = Path.GetFileName(CrossMediaManager.Current.Queue.Current.MediaUri);
                         
                // Do not update progress if it is zero. It indicates that player is stopped
                if (_currentBook.CurrentProgress.TotalMilliseconds != 0)
                {
                    _bookService.UpdateProgress(_currentBook);
                }                
            }            
        }

        public async Task JumpBack()
        {
            await CrossMediaManager.Current.StepBackward();
        }

        public async Task JumpForward()
        {
            await CrossMediaManager.Current.StepForward();
        }
    }
}
