<template>
    <div>
        <h2>Test Report</h2>
        <button @click="generateReport">Generate Report</button>
        <iframe :src="reportUrl" width="100%" height="600px"></iframe>
    </div>
</template>

<script>
  import axios from 'axios';

  export default {
    data() {
      return {
        reportUrl: ''
      };
    },
    methods: {
      async generateReport() {
        const testId = this.$route.params.testId;
        const response = await axios.get(`/api/report/generate/${testId}`, {
          responseType: 'blob'
        });
        this.reportUrl = URL.createObjectURL(new Blob([response.data], { type: 'application/pdf' }));
      }
    }
  };
</script>