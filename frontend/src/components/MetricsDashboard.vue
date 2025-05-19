<template>
    <div>
        <h2>Metrics Dashboard</h2>
        <iframe :src="metricsUrl" width="100%" height="600px"></iframe>
    </div>
</template>

<script>
  export default {
    data() {
      return {
        metricsUrl: '/api/metrics'
      };
    }
  };
</script>