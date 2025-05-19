<template>
    <div>
        <h2>Test Configuration</h2>
        <input v-model="userCount" placeholder="User Count" type="number" />
        <input v-model="requestTypes" placeholder="Request Types (comma-separated)" />
        <button @click="createTest">Create Test</button>
    </div>
</template>

<script>
  import axios from 'axios';

  export default {
    data() {
      return {
        userCount: 0,
        requestTypes: ''
      };
    },
    methods: {
      async createTest() {
        const response = await axios.post('/api/test/create', {
          userCount: this.userCount,
          requestTypes: this.requestTypes.split(',')
        });
        alert(`Test created with ID: ${response.data.testId}`);
      }
    }
  };
</script>
