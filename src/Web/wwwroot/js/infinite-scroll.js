window.InfiniteScroll = {
    _observer: null,

    initialize: function (sentinelElement, dotNetRef) {
        if (this._observer) {
            this._observer.disconnect();
        }

        this._observer = new IntersectionObserver(async (entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    await dotNetRef.invokeMethodAsync('LoadMoreItems');
                }
            }
        }, { rootMargin: '200px' });

        this._observer.observe(sentinelElement);
    },

    dispose: function () {
        if (this._observer) {
            this._observer.disconnect();
            this._observer = null;
        }
    }
};
