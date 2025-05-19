<template>
    <div>
        <h2>Run Load Test</h2>
        <button @click="runTest">Run Test</button>
    </div>
</template>

<script>
  import axios from 'axios';

  export default {
    methods: {
      async runTest() {
        const testId = this.$route.params.testId;
        const response = await axios.post(`/api/test/run/${testId}`, {
          duration: 60,
          requestRate: 10
        });
        alert('Test started!');
      }
    }
  };
</script>