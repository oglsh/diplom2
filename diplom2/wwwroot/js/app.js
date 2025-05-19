axios.defaults.baseURL = '/api';
//axios.defaults.headers.common['Content-Type'] = 'application/json';

const app = new Vue({
    el: '#app',
    data: {
        currentView: 'dashboard',
        activeTestsCount: 0,
        completedTestsToday: 0,
        testConfig: {
            name: '',
            scenario: 'custom',
            userCount: 10,
            duration: 5,
            apiCalls: [
                {
                    method: 'GET',
                    url: '',
                    body: ''
                }
            ]
        },
        savedConfig: null,
        activeTest: null,
        completedTests: [],
        selectedReportTestId: null,
        currentReport: null,
        metricsInterval: null,
        responseTimeChart: null,
        requestsChart: null,
        responseTimeDistributionChart: null,
        errorMessage: null
    },
    created() {
        this.fetchInitialData();
    },
    beforeDestroy() {
        this.clearIntervals();
    },
    methods: {
        // ==================== Инициализация ====================
        fetchInitialData() {
            this.fetchDashboardData();
            this.fetchCompletedTests();
        },

        clearIntervals() {
            if (this.metricsInterval) {
                clearInterval(this.metricsInterval);
                totalRequests = 0;
                this.metricsInterval = null;
            }
        },

        // ==================== Управление тестами ====================
        addApiCall() {
            this.testConfig.apiCalls.push({
                method: 'GET',
                url: '',
                body: ''
            });
        },

        switchView(view) {
            console.log('Switching to view:', view); // Для отладки
            this.currentView = view;

            // Инициализация данных при переходе на вкладку
            if (view === 'test-config') {
                this.initTestConfig();
            }

            if (view === 'dashboard') {
                this.fetchDashboardData();
                this.fetchCompletedTests();
            }
        },

        initTestConfig() {
            // Инициализация данных, если они пустые
            if (!this.testConfig.apiCalls || this.testConfig.apiCalls.length === 0) {
                this.testConfig = {
                    name: '',
                    scenario: 'login',
                    userCount: 10,
                    duration: 5,
                    apiCalls: []
                };
            }
        },


        removeApiCall(index) {
            this.testConfig.apiCalls.splice(index, 1);
        },

        async saveTestConfig() {
            try {
                const scenario = {
                    name: this.testConfig.name,
                    userCount: this.testConfig.userCount,
                    duration: this.testConfig.duration,
                    apiCalls: this.testConfig.apiCalls.map(call => ({
                        method: call.method,
                        url: call.url,
                        body: call.body 
                    }))

                };
                let testResp = await axios.get('/tests/test')
                const response = await axios.post('/tests/create', scenario);
                this.savedConfig = {
                    id: response.data.testId,
                    name: this.testConfig.name
                };
                this.showSuccess('Конфигурация теста успешно сохранена');
            } catch (error) {
                console.error('Ошибка сохранения конфигурации:', error);
                this.showError('Ошибка при сохранении конфигурации');
            }
        },

        async startTest() {
            if (!this.savedConfig?.id) return;

            try {
                const config = {
                    requestRate: this.testConfig.userCount,
                    duration: this.testConfig.duration
                };

                this.activeTest = {
                    id: this.savedConfig.id,
                    name: this.savedConfig.name,
                    progress: 0

                };

                this.showSuccess('Тест успешно запущен');
                this.activeTestsCount++;
                await this.startMonitoring();
                await axios.post(`/tests/${this.savedConfig.id}/run`, config);
                //this.showSuccess('Тест завершен');
                //this.activeTestsCount--;

            } catch (error) {
                if (error.response?.data?.errors) {
                    this.errors = error.response.data.errors;
                }
                console.error('Ошибка запуска теста:', error);
                this.showError('Не удалось запустить тест');
            }
        },

        async stopTest() {
            if (!this.activeTest) return;

            try {
                await axios.post(`/tests/${this.activeTest.id}/stop`);
                this.clearIntervals();
                this.activeTest = null;
                this.activeTestsCount--;
                this.fetchCompletedTests();
                this.showSuccess('Тест успешно остановлен');
            } catch (error) {
                console.error('Ошибка остановки теста:', error);
                this.showError('Не удалось остановить тест');
            }
        },

        // ==================== Мониторинг и метрики ====================
        async startMonitoring() {
            this.clearIntervals();
            this.metricsInterval = setInterval(async () => {
                try {
                    const metrics = await this.fetchMetrics();
                    const totalRequests = this.parseTestProgress(metrics);

                    if (this.activeTest) {
                        // Рассчитываем прогресс на основе общего количества запросов
                        const expectedRequests = this.testConfig.userCount * this.testConfig.duration;
                        this.activeTest.progress = Math.min(
                            Math.round((totalRequests / expectedRequests) * 100),
                            100
                        );

                        if (this.activeTest.progress >= 100) {
                            this.clearIntervals();
                            this.stopTest();
                            this.activeTest.progress = 0;
                            totalRequests = 0;
                        }
                    }
                } catch (error) {
                    console.error('Ошибка мониторинга:', error);
                }
            }, 1000);
        },

        async fetchMetrics() {
            try {
                const response = await axios.get('/metrics');
                return response.data;
            } catch (error) {
                console.error('Ошибка получения метрик:', error);
                return 'completedRequests: 0 ';
            }
        },

        parseTestProgress(metricsText) {
            const lines = metricsText.split('\n');
            let totalRequests = 0;

            lines.forEach(line => {
                if (line.startsWith('http_requests_total')) {
                    const parts = line.split(' ');
                    const value = parseFloat(parts[parts.length - 1]);
                    if (!isNaN(value)) {
                        totalRequests += value;
                    }
                }
            });

            return totalRequests;
        },

        // ==================== Отчеты ====================
        async fetchCompletedTests() {
            try {
                const response = await axios.get('/tests/completed');
                this.completedTests = response.data;
            } catch (error) {
                console.error('Ошибка получения списка тестов:', error);
                this.showError('Не удалось загрузить список тестов');
            }
        },

        async loadReport() {
            if (!this.selectedReportTestId) {
                this.showError('Please select a test from the list');
                return;
            }

            try {
                const response = await axios.get(`/tests/${this.selectedReportTestId}/completedTest/`);
                this.currentReport = response.data;

            } catch (error) {
                console.error('Error loading report:', error);
                this.showError('Failed to load report');
            }
        },

        async exportReport() {
            if (!this.selectedReportTestId) {
                this.showError('Пожалуйста, выберите тест из списка');
                return;
            }

            try {
                const response = await axios.get(`/reports/generate/${this.selectedReportTestId}`, {
                    responseType: 'blob'
                });

                const url = URL.createObjectURL(response.data);
                const link = document.createElement('a');
                link.href = url;
                link.setAttribute('download', `report_${this.completedTests.name} - ${this.completedTests.endDate}.pdf`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            } catch (error) {
                console.error('Ошибка загрузки отчета:', error);
                this.showError('Не удалось загрузить отчет');
            }
        },

        // ==================== Дашборд ====================
        async fetchDashboardData() {
            try {
                const [metricsResponse, statsResponse] = await Promise.all([
                    axios.get('/metrics'),
                    axios.get('/stats/getStats')
                ]);

                this.activeTestsCount = statsResponse.data.activeTests || 0;
                this.completedTestsToday = statsResponse.data.completedToday || 0;

                if (metricsResponse.data) {
                    this.renderResponseTimeChart(this.parseResponseMetrics(metricsResponse.data));
                }
            } catch (error) {
                console.error('Ошибка загрузки дашборда:', error);
            }
        },

        parseResponseMetrics(metricsText) {
            // Пример парсинга метрик времени ответа
            const responseTimes = [];
            const lines = metricsText.split('\n');

            lines.forEach(line => {
                if (line.startsWith('http_response_time_ms_bucket')) {
                    const value = parseFloat(line.split(' ')[1]);
                    if (!isNaN(value)) {
                        responseTimes.push(value);
                    }
                }
            });

            return {
                labels: Array.from({ length: responseTimes.length }, (_, i) => `${i * 5}s`),
                values: responseTimes
            };
        },

        // ==================== Визуализация ====================
        renderResponseTimeChart(data) {
            const ctx = document.getElementById('responseTimeChart')?.getContext('2d');
            if (!ctx) return;

            if (this.responseTimeChart) {
                this.responseTimeChart.destroy();
            }

            this.responseTimeChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: data.labels,
                    datasets: [{
                        label: 'Время ответа (мс)',
                        data: data.values,
                        borderColor: 'rgba(75, 192, 192, 1)',
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        borderWidth: 1,
                        tension: 0.1
                    }]
                },
                options: {
                    responsive: true,
                    scales: {
                        y: {
                            beginAtZero: true,
                            title: {
                                display: true,
                                text: 'Миллисекунды'
                            }
                        },
                        x: {
                            title: {
                                display: true,
                                text: 'Время тестирования'
                            }
                        }
                    }
                }
            });
        },

        // ==================== Утилиты ====================
        showError(message) {
            this.errorMessage = message;
            setTimeout(() => {
                this.errorMessage = null;
            }, 5000);
        },

        showSuccess(message) {
            alert(message); // Можно заменить на красивые уведомления
        },

        formatDate(dateString) {
            if (!dateString) return '';
            const options = { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' };
            return new Date(dateString).toLocaleDateString(undefined, options);
        }
    }
});