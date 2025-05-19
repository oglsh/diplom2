import Vue from 'vue';
import Router from 'vue-router';
import TestConfiguration from '@/components/TestConfiguration.vue';
import LoadTest from '@/components/LoadTest.vue';
import MetricsDashboard from '@/components/MetricsDashboard.vue';
import ReportViewer from '@/components/ReportViewer.vue';

Vue.use(Router);

export default new Router({
    routes: [
        { path: '/', component: TestConfiguration },
        { path: '/test/:testId/run', component: LoadTest },
        { path: '/metrics', component: MetricsDashboard },
        { path: '/report/:testId', component: ReportViewer }
    ]
});