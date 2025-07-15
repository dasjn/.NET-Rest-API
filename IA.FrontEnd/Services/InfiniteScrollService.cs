namespace IA.FrontEnd.Services
{
    public class InfiniteScrollService<T>
    {
        private readonly List<T> _items = new();
        private int _currentSkip = 0;
        private readonly int _pageSize = 10;
        private bool _isLoading = false;
        private bool _hasReachedEnd = false;

        public IReadOnlyList<T> Items => _items.AsReadOnly();
        public bool IsLoading => _isLoading;
        public bool HasReachedEnd => _hasReachedEnd;
        public int TotalLoaded => _items.Count;

        public event Action? OnStateChanged;

        public async Task LoadInitialData(Func<int, int, Task<List<T>>> loadFunction)
        {
            Console.WriteLine("🔄 LoadInitialData called");
            await Reset();
            await LoadMoreData(loadFunction);
        }

        public async Task LoadMoreData(Func<int, int, Task<List<T>>> loadFunction)
        {
            if (_isLoading || _hasReachedEnd)
            {
                Console.WriteLine($"⏭️ Skipping LoadMoreData - IsLoading: {_isLoading}, HasReachedEnd: {_hasReachedEnd}");
                return;
            }

            Console.WriteLine($"📥 Loading more data - Skip: {_currentSkip}, Take: {_pageSize}");
            _isLoading = true;
            OnStateChanged?.Invoke();

            try
            {
                var newItems = await loadFunction(_currentSkip, _pageSize);
                Console.WriteLine($"📊 Loaded {newItems?.Count ?? 0} new items");

                if (newItems == null || newItems.Count == 0)
                {
                    _hasReachedEnd = true;
                    Console.WriteLine("🔚 Reached end - no more items");
                }
                else
                {
                    _items.AddRange(newItems);
                    _currentSkip += newItems.Count;
                    Console.WriteLine($"✅ Added items. Total: {_items.Count}, Next skip: {_currentSkip}");

                    // Si recibimos menos elementos de los solicitados, hemos llegado al final
                    if (newItems.Count < _pageSize)
                    {
                        _hasReachedEnd = true;
                        Console.WriteLine($"🔚 Reached end - got {newItems.Count} items, expected {_pageSize}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading more data: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                OnStateChanged?.Invoke();
            }
        }

        public async Task Reset()
        {
            Console.WriteLine("🔄 Resetting infinite scroll service");
            _items.Clear();
            _currentSkip = 0;
            _hasReachedEnd = false;
            _isLoading = false;
            OnStateChanged?.Invoke();
        }

        public async Task Search(string query, Func<string, int, int, Task<List<T>>> searchFunction)
        {
            await Reset();
            await LoadMoreDataWithSearch(query, searchFunction);
        }

        public async Task LoadMoreDataWithSearch(string query, Func<string, int, int, Task<List<T>>> searchFunction)
        {
            if (_isLoading || _hasReachedEnd)
                return;

            _isLoading = true;
            OnStateChanged?.Invoke();

            try
            {
                var newItems = await searchFunction(query, _currentSkip, _pageSize);

                if (newItems == null || newItems.Count == 0)
                {
                    _hasReachedEnd = true;
                }
                else
                {
                    _items.AddRange(newItems);
                    _currentSkip += newItems.Count;

                    if (newItems.Count < _pageSize)
                    {
                        _hasReachedEnd = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching more data: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                OnStateChanged?.Invoke();
            }
        }
    }
}